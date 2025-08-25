using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Mail;
using System.Windows;
using System.Windows.Controls;
using System.IO;

namespace WpfApp1
{
    public partial class OrderCardWindow : Window
    {
        private const string senderEmail = "yabank2025@gmail.com";
        private const string senderPassword = "yruhfvsxhshgpovq";

        private readonly DbManager _db = new DbManager();
        private readonly int _userId;
        private string _lastContractFilePath = string.Empty;

        private readonly Dictionary<string, decimal> _cardPrices = new()
        {
            { "Стандартная", 10 },
            { "Золотая", 25 },
            { "Платиновая", 50 }
        };

        public OrderCardWindow() : this(0) { }

        public OrderCardWindow(int userId)
        {
            InitializeComponent();
            _userId = userId;
            LoadUserCards();
            CardTypeComboBox.SelectionChanged += (s, e) => UpdatePrice();
        }

        private async void LoadUserCards()
        {
            var cards = await _db.GetFullUserCardsAsync(_userId);
            PaymentCardComboBox.ItemsSource = cards;
            if (cards.Count > 0)
                PaymentCardComboBox.SelectedIndex = 0;
        }

        private void UpdatePrice()
        {
            if (CardTypeComboBox.SelectedItem is ComboBoxItem selected)
            {
                string type = selected.Content.ToString();
                if (_cardPrices.TryGetValue(type, out decimal price))
                    PriceTextBlock.Text = $"{price:F2} BYN";
            }
        }

        private async void OrderCardButton_Click(object sender, RoutedEventArgs e)
        {
            string fullName = FullNameTextBox.Text.Trim().ToUpper();
            string cardType = (CardTypeComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();

            if (string.IsNullOrWhiteSpace(fullName) || cardType == null)
            {
                MessageBox.Show("Пожалуйста, заполните все поля.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var nameParts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (nameParts.Length < 2)
            {
                MessageBox.Show("Введите как минимум фамилию и имя (например: SMITH JOHN).", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string cardHolderName = $"{nameParts[0]} {nameParts[1]}";

            if (PaymentCardComboBox.SelectedItem is not CardInfo selectedCard)
            {
                MessageBox.Show("Выберите карту для оплаты.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            decimal price = _cardPrices[cardType];
            if (selectedCard.Balance < price)
            {
                MessageBox.Show("Недостаточно средств. Выберите другую карту.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DateTime expiry = cardType switch
            {
                "Стандартная" => DateTime.Now.AddYears(2),
                "Золотая" => DateTime.Now.AddYears(4),
                "Платиновая" => DateTime.Now.AddYears(5),
                _ => throw new NotImplementedException()
            };

            string plainNumber = GeneratePlainCardNumber();
            string formattedNumber = FormatCardNumber(plainNumber);
            string cvv = new Random().Next(100, 1000).ToString();
            int randomBalance = new Random().Next(200, 1001);

            var insertParams = new Dictionary<string, object>
            {
                { "@Number", plainNumber },
                { "@CardName", cardType },
                { "@Cardholder", cardHolderName },
                { "@Duration", expiry },
                { "@CVV", cvv },
                { "@Balance", randomBalance }
            };

            string insertCard = @"INSERT INTO Cards (Number, CardName, Cardholder, Duration, CVV, Balance) 
                                  VALUES (@Number, @CardName, @Cardholder, @Duration, @CVV, @Balance);
                                  SELECT SCOPE_IDENTITY();";

            int cardId = Convert.ToInt32(await _db.ExecuteScalarAsync(insertCard, insertParams));

            await _db.ExecuteWithParamsAsync("INSERT INTO UserCards (UserID, CardsID) VALUES (@UserID, @CardsID)",
                new Dictionary<string, object> { { "@UserID", _userId }, { "@CardsID", cardId } });

            await _db.UpdateCardBalanceAsync(selectedCard.CardsID, -price);

            await _db.RecordTransactionAsync(new Transaction
            {
                UserID = _userId,
                TransactionType = "CardOrder",
                Amount = price,
                SenderCardID = selectedCard.CardsID,
                RecipientDetails = $"New {cardType} card",
                Details = $"Заказ карты {cardType} ({cardHolderName})",
                Status = "Completed"
            });

            CreateContractDocument(cardType, fullName, formattedNumber, expiry);

            MessageBox.Show($"Карта заказана:\n\nНомер: {formattedNumber}\nДержатель: {cardHolderName}\nCVV: {cvv}\nСрок: {expiry:MM/yyyy}",
                "Карта оформлена", MessageBoxButton.OK, MessageBoxImage.Information);

            string userEmail = await _db.GetUserEmailAsync(_userId);
            if (!string.IsNullOrEmpty(userEmail))
            {
                await SendCardDetailsEmail(userEmail, formattedNumber, cardHolderName, cvv, expiry, cardType, _lastContractFilePath);
            }

            // Найти открытое окно HomeWindow и обновить в нём данные
            foreach (Window window in Application.Current.Windows)
            {
                if (window is HomeWindow homeWindow)
                {
                    homeWindow.RefreshCards();
                    break;
                }
            }

            this.Close();
        }

        private void CreateContractDocument(string cardType, string fullName, string cardNumber, DateTime expiry)
        {
            string fileName = $"Договор_на_карту_{cardNumber}_{DateTime.Now:yyyyMMdd_HHmmss}.docx";
            string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), fileName);

            using (var doc = Novacode.DocX.Create(filePath))
            {
                doc.InsertParagraph("ДОГОВОР НА ВЫДАЧУ БАНКОВСКОЙ КАРТЫ").FontSize(18).Bold().Alignment = Novacode.Alignment.center;
                doc.InsertParagraph($"\nПользователь {fullName} оформил заказ на банковскую карту типа '{cardType}'.\n")
                    .FontSize(12).SpacingAfter(10);
                doc.InsertParagraph($"Номер карты: {cardNumber}").FontSize(12);
                doc.InsertParagraph($"Срок действия: {expiry:MM/yyyy}").FontSize(12);
                doc.InsertParagraph("\n\nПодпись: __________________________\nДата: " + DateTime.Now.ToString("dd.MM.yyyy"))
                    .FontSize(12);
                doc.Save();
            }

            _lastContractFilePath = filePath;
        }

        private string GeneratePlainCardNumber()
        {
            var rnd = new Random();
            return string.Concat(Enumerable.Range(0, 16).Select(_ => rnd.Next(0, 10).ToString()));
        }

        private string FormatCardNumber(string plainNumber)
        {
            return string.Join(" ", Enumerable.Range(0, 4).Select(i => plainNumber.Substring(i * 4, 4)));
        }

        private async Task SendCardDetailsEmail(string email, string number, string holder, string cvv, DateTime expiry, string cardType, string contractPath)
        {
            string html = $@"
                <html>
                    <body style='font-family: Arial; padding: 20px;'>
                        <h2 style='color:#2e6de6;'>Ваша карта успешно заказана!</h2>
                        <p>Тип карты: <strong>{cardType}</strong></p>
                        <p>Номер карты: <strong>{number}</strong></p>
                        <p>Держатель: <strong>{holder}</strong></p>
                        <p>Срок действия: <strong>{expiry:MM/yyyy}</strong></p>
                        <p>CVV: <strong>{cvv}</strong></p>
                        <hr />
                        <small style='color:gray;'>Не сообщайте эти данные третьим лицам.</small>
                    </body>
                </html>";

            MailMessage mail = new MailMessage(senderEmail, email)
            {
                Subject = "Данные вашей новой карты",
                Body = html,
                IsBodyHtml = true
            };

            // Убедиться, что файл действительно завершён созданием
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    using (FileStream fs = File.Open(contractPath, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                        // Файл открыт успешно, значит он не занят и готов
                        fs.Close();
                        break;
                    }
                }
                catch (IOException)
                {
                    await Task.Delay(100); // Подождать 100 мс и попробовать снова
                }
            }

            // Проверка и прикрепление
            if (File.Exists(contractPath))
            {
                Attachment attachment = new Attachment(contractPath);
                mail.Attachments.Add(attachment);
            }

            using var client = new SmtpClient("smtp.gmail.com", 587)
            {
                Credentials = new NetworkCredential(senderEmail, senderPassword),
                EnableSsl = true
            };

            await client.SendMailAsync(mail);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();

            // Найти открытое главное окно и обновить его
            foreach (Window window in Application.Current.Windows)
            {
                if (window is HomeWindow homeWindow)
                {
                    homeWindow.RefreshCards(); // вызываем метод обновления
                    homeWindow.Show(); // на случай, если окно было скрыто
                    return;
                }
            }

            // Если главное окно не было открыто — открыть заново
            var newHomeWindow = new HomeWindow(SessionManager.CurrentUserID);
            newHomeWindow.Show();
        }

        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb && tb == FullNameTextBox)
                FullNameHint.Visibility = Visibility.Collapsed;
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            UpdateHints();
        }

        private void UpdateHints()
        {
            FullNameHint.Visibility = string.IsNullOrWhiteSpace(FullNameTextBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }
}
