using System.Net;
using System.Net.Mail;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace WpfApp1
{
    public partial class CardToCardWindow : Window
    {
        private readonly DbManager _db = new DbManager();

        private const string senderEmail = "yabank2025@gmail.com";
        private const string senderPassword = "yruhfvsxhshgpovq";

        private readonly int _userId;

        public CardToCardWindow(int userId)
        {
            InitializeComponent();

            if (userId <= 0)
                throw new ArgumentException("UserID должен быть положительным и существующим.", nameof(userId));

            _userId = userId;

            // Подписка на события для подсказок (необязательно)

            LoadUserCards();

            // Исчезание надписей
            CardNumberTextBox.GotFocus += (s, e) => CardNumberTextBlock.Visibility = Visibility.Collapsed;
            CardNumberTextBox.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(CardNumberTextBox.Text))
                    CardNumberTextBlock.Visibility = Visibility.Visible;
            };

            AmountTextBox.GotFocus += (s, e) => AmountTextBlock.Visibility = Visibility.Collapsed;
            AmountTextBox.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(AmountTextBox.Text))
                    AmountTextBlock.Visibility = Visibility.Visible;
            };
        }

        private async void LoadUserCards()
        {
            try
            {
                var cards = await _db.GetFullUserCardsAsync(_userId);
                CardComboBox.ItemsSource = cards;

                if (cards.Count > 0)
                    CardComboBox.SelectedIndex = 0;
                else
                    MessageBox.Show("У пользователя нет доступных карт для перевода.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки карт: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Форматирование номера карты с пробелами и курсор в конец
        private void CardNumberTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                int caretPos = tb.CaretIndex;
                string text = tb.Text.Replace(" ", "");
                if (text.Length > 16) text = text.Substring(0, 16);

                var formatted = new StringBuilder();
                for (int i = 0; i < text.Length; i++)
                {
                    if (i > 0 && i % 4 == 0) formatted.Append(' ');
                    formatted.Append(text[i]);
                }

                string newText = formatted.ToString();

                if (tb.Text != newText)
                {
                    tb.Text = newText;
                    tb.CaretIndex = tb.Text.Length; // курсор в конец
                }
                else
                {
                    tb.CaretIndex = caretPos; // если изменений нет, курсор не трогаем
                }
            }
        }

        private async void TopUpButton_Click(object sender, RoutedEventArgs e)
        {
            string receiverCardNumber = CardNumberTextBox.Text.Replace(" ", "").Trim();
            string amountText = AmountTextBox.Text.Trim();

            if (CardComboBox.SelectedItem is not CardInfo selectedCard)
            {
                MessageBox.Show("Выберите карту для списания средств.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrEmpty(receiverCardNumber) || string.IsNullOrEmpty(amountText))
            {
                MessageBox.Show("Заполните все поля.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(amountText, out decimal amount) || amount <= 0)
            {
                MessageBox.Show("Введите корректную сумму перевода.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool success = false;
            string errorMessage = string.Empty;

            try
            {
                // Получаем баланс карты отправителя
                string queryBalance = "SELECT Balance FROM Cards WHERE CardsID = @CardID";
                var param = new Dictionary<string, object> { { "@CardID", selectedCard.CardsID } };
                decimal senderBalance = Convert.ToDecimal(await _db.ExecuteScalarAsync(queryBalance, param));

                if (senderBalance >= amount)
                {
                    // Списываем сумму
                    string updateQuery = "UPDATE Cards SET Balance = Balance - @Amount WHERE CardsID = @CardID";
                    var updateParams = new Dictionary<string, object>
                    {
                        {"@Amount", amount},
                        {"@CardID", selectedCard.CardsID}
                    };
                    int rows = await _db.ExecuteWithParamsAsync(updateQuery, updateParams);

                    if (rows > 0)
                        success = true;
                    else
                        errorMessage = "Не удалось списать средства с карты.";
                }
                else
                {
                    errorMessage = "Недостаточно средств на карте.";
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"Ошибка при выполнении операции: {ex.Message}";
            }

            // Записываем транзакцию с учетом результата
            await _db.RecordTransactionAsync(new Transaction
            {
                UserID = _userId,
                TransactionType = "Перевод на карту",
                Amount = amount,
                SenderCardID = selectedCard.CardsID,
                RecipientDetails = receiverCardNumber,
                Details = $"Пополнение/оплата по реквизиту: {receiverCardNumber}",
                Status = success ? "Completed" : "Failed"
            });

            // Отправка письма с чеком (даже если неуспешно)
            try
            {
                string userEmail = await _db.GetUserEmailAsync(_userId);
                if (!string.IsNullOrEmpty(userEmail))
                {
                    await SendReceiptEmail(userEmail, amount, selectedCard.Number, receiverCardNumber, success);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при отправке чека: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            if (success)
            {
                MessageBox.Show("Перевод выполнен успешно.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                // Обновляем главное окно — ищем открытое и вызываем метод обновления
                foreach (Window win in Application.Current.Windows)
                {
                    if (win is HomeWindow home)
                    {
                        home.RefreshCards(); // ваш метод обновления
                        break;
                    }
                }

                this.Close();
            }
            else
            {
                MessageBox.Show(errorMessage, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async Task SendReceiptEmail(string recipientEmail, decimal amount, string senderCard, string receiverCard, bool success)
        {
            try
            {
                string maskedSender = $"****{senderCard[^4..]}";
                string maskedReceiver = $"****{receiverCard[^4..]}";
                string date = DateTime.Now.ToString("dd.MM.yyyy HH:mm");

                string subject = success ? "Чек: перевод между картами" : "Ошибка: перевод между картами";
                string statusText = success ? "Перевод выполнен успешно." : "Перевод не выполнен из-за недостатка средств.";

                using var mail = new MailMessage(senderEmail, recipientEmail)
                {
                    Subject = subject,
                    Body = $@"
<html>
<head><style>
    /* стили тут */
</style></head>
<body>
<div>
<h2>{subject}</h2>
<p style='font-weight:bold;'>{statusText}</p>
<div><b>Сумма перевода:</b> {amount:F2} BYN</div>
<div><b>С карты:</b> {maskedSender}</div>
<div><b>На карту:</b> {maskedReceiver}</div>
<div><b>Дата:</b> {date}</div>
<hr />
<p>Это автоматическое письмо. Пожалуйста, не отвечайте на него.</p>
<p>&copy; {DateTime.Now.Year} YaBank</p>
</div>
</body>
</html>",
                    IsBodyHtml = true
                };

                using var smtp = new SmtpClient("smtp.gmail.com", 587)
                {
                    Credentials = new NetworkCredential(senderEmail, senderPassword),
                    EnableSsl = true
                };

                await smtp.SendMailAsync(mail);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при отправке письма: {ex.Message}", "Ошибка почты", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
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
    }
}
