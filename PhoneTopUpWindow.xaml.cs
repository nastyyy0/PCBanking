using System;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WpfApp1
{
    public partial class PhoneTopUpWindow : Window
    {
        private readonly DbManager _db = new DbManager();
        private readonly int _userId;

        // Данные отправителя
        private const string senderEmail = "yabank2025@gmail.com";
        private const string senderPassword = "yruhfvsxhshgpovq";

        public PhoneTopUpWindow(int userId)
        {
            InitializeComponent();
            _userId = userId;
            LoadUserCards();

            PhoneNumberTextBox.GotFocus += (s, e) => PhoneNumberTextBlock.Visibility = Visibility.Collapsed;
            PhoneNumberTextBox.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(PhoneNumberTextBox.Text))
                    PhoneNumberTextBlock.Visibility = Visibility.Visible;
            };

            AmountTextBox.GotFocus += (s, e) => AmountTextBlock.Visibility = Visibility.Collapsed;
            AmountTextBox.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(AmountTextBox.Text))
                    AmountTextBlock.Visibility = Visibility.Visible;
            };

            // Закрываем HomeWindow, если оно открыто
            foreach (Window window in Application.Current.Windows)
            {
                if (window is HomeWindow homeWindow)
                {
                    homeWindow.Close();
                    break;
                }
            }
        }
            
        private async void LoadUserCards()
        {
            try
            {
                var cards = await _db.GetFullUserCardsAsync(_userId);
                CardComboBox.ItemsSource = cards;

                if (cards.Count > 0)
                    CardComboBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки карт: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool IsValidBelarusPhoneNumber(string phone)
        {
            return Regex.IsMatch(phone, @"^(29|25|44|33)\d{7}$");
        }

        private void PhoneNumberTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, @"^\d+$") || PhoneNumberTextBox.Text.Length >= 9;
        }

        private void AmountTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            bool isDot = e.Text == ".";
            bool containsDot = AmountTextBox.Text.Contains(".");

            e.Handled = !Regex.IsMatch(e.Text, @"^[\d.]+$") ||
                        (isDot && containsDot) ||
                        (containsDot && AmountTextBox.Text.Split('.')[1].Length >= 2);
        }

        private async void TopUpButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(PhoneNumberTextBox.Text))
            {
                MessageBox.Show("Введите номер телефона", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!IsValidBelarusPhoneNumber(PhoneNumberTextBox.Text))
            {
                MessageBox.Show("Введите корректный белорусский номер (29, 25, 33 или 44)",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(AmountTextBox.Text) ||
                !decimal.TryParse(AmountTextBox.Text, out decimal amount) ||
                amount <= 0)
            {
                MessageBox.Show("Введите корректную сумму пополнения", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (CardComboBox.SelectedItem == null)
            {
                MessageBox.Show("Выберите карту для оплаты", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedCard = CardComboBox.SelectedItem as CardInfo;
            string phoneNumber = PhoneNumberTextBox.Text;

            try
            {
                if (selectedCard.Balance < amount)
                {
                    MessageBox.Show("Недостаточно средств на выбранной карте", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Списание средств
                bool success = await _db.UpdateCardBalanceAsync(selectedCard.CardsID, -amount);
                if (!success)
                {
                    MessageBox.Show("Ошибка при списании средств", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Запись транзакции
                await _db.RecordTransactionAsync(new Transaction
                {
                    UserID = _userId,
                    TransactionType = "Оплата телефона",
                    Amount = amount,
                    SenderCardID = selectedCard.CardsID,
                    RecipientDetails = phoneNumber,
                    Details = $"Пополнение номера {FormatPhoneNumber(phoneNumber)}",
                    Status = "Completed"
                });

                // Получаем email и отправляем чек + открытие окна
                string userEmail = await _db.GetUserEmailAsync(_userId);
                if (!string.IsNullOrEmpty(userEmail))
                {
                    await SendReceiptEmail(
                        userEmail,
                        amount,
                        phoneNumber,
                        selectedCard.Number,
                        selectedCard.CardName
                    );
                }
                else
                {
                    MessageBox.Show("Не удалось отправить чек: email пользователя не найден",
                                  "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                if (success)
                {
                    MessageBox.Show("Операция выполнена.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    SessionManager.CurrentUserID = _userId;
                    HomeWindow homeWindow = new HomeWindow(_userId);
                    homeWindow.Show();
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка операции: {ex.Message}",
                              "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task SendReceiptEmail(string recipientEmail, decimal amount, string phoneNumber, string cardNumber, string cardName)
        {
            try
            {
                string maskedCardNumber = FormatCardNumber(cardNumber);
                string displayPhone = FormatPhoneNumber(phoneNumber);
                string transactionDate = DateTime.Now.ToString("dd.MM.yyyy HH:mm");

                using var mail = new MailMessage(senderEmail, recipientEmail)
                {
                    Subject = $"Чек о пополнении телефона {displayPhone}",
                    Body = GetReceiptHtml(amount, displayPhone, maskedCardNumber, transactionDate),
                    IsBodyHtml = true
                };

                using var client = new SmtpClient("smtp.gmail.com", 587)
                {
                    Credentials = new NetworkCredential(senderEmail, senderPassword),
                    EnableSsl = true,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    Timeout = 10000
                };

                await client.SendMailAsync(mail);

                // Открытие окна чека
                var receiptWindow = new ReceiptWindow(new TransactionViewModel
                {
                    CardName = cardName,
                    CardNumber = maskedCardNumber,
                    TransactionType = "Оплата телефона",
                    Amount = amount,
                    TransactionDate = DateTime.Now,
                    Status = "Completed",
                    Details = $"Номер телефона: {displayPhone}"
                });
                receiptWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                string errorMessage = $"Ошибка при отправке чека: {ex.Message}";
                MessageBox.Show(errorMessage, "Ошибка отправки письма", MessageBoxButton.OK, MessageBoxImage.Warning);

                string logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "email_errors.log");
                await File.AppendAllTextAsync(logPath, $"{DateTime.Now}: {errorMessage}{Environment.NewLine}");
            }
        }

        private string FormatCardNumber(string cardNumber)
        {
            return string.Join(" ", Regex.Matches(cardNumber, @"\d{1,4}").Select(m => m.Value));
        }

        private string FormatPhoneNumber(string phone)
        {
            return $"+375 ({phone.Substring(0, 2)}) {phone.Substring(2, 3)}-{phone.Substring(5, 2)}-{phone.Substring(7)}";
        }

        private string GetReceiptHtml(decimal amount, string phoneNumber, string cardNumber, string transactionDate)
        {
            return $@"
                <html>
                    <head>
                        <style>
                            body {{ font-family: Arial, sans-serif; background-color: #f5f5f5; padding: 20px; }}
                            .receipt {{ max-width: 500px; margin: 0 auto; background-color: white; padding: 25px; border-radius: 10px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }}
                            .header {{ color: #2a5885; text-align: center; margin-bottom: 20px; }}
                            .details {{ margin: 20px 0; }}
                            .detail-row {{ display: flex; justify-content: space-between; margin-bottom: 10px; }}
                            .detail-label {{ font-weight: bold; color: #555; }}
                            .footer {{ margin-top: 30px; font-size: 12px; color: #999; text-align: center; }}
                            .amount {{ font-size: 24px; font-weight: bold; color: #2a5885; text-align: center; margin: 20px 0; }}
                        </style>
                    </head>
                    <body>
                        <div class='receipt'>
                            <h2 class='header'>Чек о пополнении телефона</h2>
                            
                            <div class='amount'>{amount} BYN</div>
                            
                            <div class='details'>
                                <div class='detail-row'>
                                    <span class='detail-label'>Номер телефона:</span>
                                    <span>{phoneNumber}</span>
                                </div>
                                <div class='detail-row'>
                                    <span class='detail-label'>Карта списания:</span>
                                    <span>{cardNumber}</span>
                                </div>
                                <div class='detail-row'>
                                    <span class='detail-label'>Дата операции:</span>
                                    <span>{transactionDate}</span>
                                </div>
                                <div class='detail-row'>
                                    <span class='detail-label'>Тип операции:</span>
                                    <span>Пополнение мобильного</span>
                                </div>
                            </div>
                            
                            <div class='footer'>
                                <p>Это письмо сформировано автоматически. Пожалуйста, не отвечайте на него.</p>
                                <p>© {DateTime.Now.Year} YaBank. Все права защищены.</p>
                            </div>
                        </div>
                    </body>
                </html>";
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
            var newHomeWindow = new HomeWindow(_userId);
            newHomeWindow.Show();
        }
    }
}