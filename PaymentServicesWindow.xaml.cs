using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Text.RegularExpressions;

namespace WpfApp1
{
    public partial class PaymentServicesWindow : Window
    {
        private readonly DbManager _db = new DbManager();
        private readonly int _userId;

        private const string senderEmail = "yabank2025@gmail.com";
        private const string senderPassword = "yruhfvsxhshgpovq";

        public PaymentServicesWindow(int userId)
        {
            InitializeComponent();
            _userId = userId;
            Loaded += PaymentServicesWindow_Loaded;
        }

        private async void PaymentServicesWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                var cards = await _db.GetFullUserCardsAsync(_userId);
                CardComboBox.ItemsSource = cards;
                CardComboBox.DisplayMemberPath = "CardName";
                CardComboBox.SelectedValuePath = "CardsID";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки карт: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void TopUpButton_Click(object sender, RoutedEventArgs e)
        {
            string contractNumber = ContractNumberTextBox.Text.Trim();
            string amountText = AmountTextBox.Text.Trim();

            if (contractNumber.Length != 13 || !contractNumber.All(char.IsDigit))
            {
                MessageBox.Show("Номер договора должен содержать ровно 13 цифр.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(amountText, out decimal amount) || amount <= 0)
            {
                MessageBox.Show("Введите корректную сумму оплаты.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (CardComboBox.SelectedItem is not CardInfo selectedCard)
            {
                MessageBox.Show("Выберите карту для оплаты.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var parametersBalance = new Dictionary<string, object> { { "@CardsID", selectedCard.CardsID } };
                object resultBalance = await _db.ExecuteScalarAsync("SELECT Balance FROM Cards WHERE CardsID = @CardsID", parametersBalance);
                if (resultBalance == null || resultBalance == DBNull.Value)
                {
                    MessageBox.Show("Не удалось получить баланс карты.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                decimal balance = Convert.ToDecimal(resultBalance);

                if (balance < amount)
                {
                    MessageBox.Show("Недостаточно средств на карте.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var queries = new List<string> { "UPDATE Cards SET Balance = Balance - @Amount WHERE CardsID = @CardsID" };
                var paramsList = new List<Dictionary<string, object>>
                {
                    new Dictionary<string, object> { { "@Amount", amount }, { "@CardsID", selectedCard.CardsID } }
                };

                bool success = await _db.ExecuteTransactionAsync(queries, paramsList);

                var transaction = new Transaction
                {
                    UserID = _userId,
                    TransactionType = "Оплата услуг",
                    Amount = amount,
                    SenderCardID = selectedCard.CardsID,
                    RecipientDetails = contractNumber,
                    Details = $"Оплата услуги, договор № {contractNumber}",
                    Status = success ? "Completed" : "Failed"
                };
                await _db.RecordTransactionAsync(transaction);

                string userEmail = await _db.GetUserEmailAsync(_userId);

                if (string.IsNullOrWhiteSpace(userEmail))
                {
                    MessageBox.Show("Не удалось получить email пользователя для отправки чека.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                await SendReceiptEmail(userEmail, amount, selectedCard.Number, contractNumber, success);

                if (success)
                {
                    // Найти открытое окно HomeWindow и обновить в нём данные
                    foreach (Window window in Application.Current.Windows)
                    {
                        if (window is HomeWindow homeWindow)
                        {
                            homeWindow.RefreshCards();
                            break;
                        }
                    }

                    MessageBox.Show("Оплата успешно выполнена. Чек отправлен на почту.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    this.Close();
                }
                else
                {
                    MessageBox.Show("Ошибка при выполнении оплаты. Чек отправлен на почту.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task SendReceiptEmail(string toEmail, decimal amount, string cardNumber, string contractNumber, bool success)
        {
            string statusText = success ? "ОПЛАТА ПРОШЛА УСПЕШНО" : "ОШИБКА ПРИ ОПЛАТЕ";
            string transactionDate = DateTime.Now.ToString("dd.MM.yyyy HH:mm");
            string subject = success ? "Чек об оплате услуги" : "Ошибка при оплате услуги";
            string body = $@"<html>
                                <body>
                                    <h2>{statusText}</h2>
                                    <p>Номер договора: {contractNumber}</p>
                                    <p>Карта: {cardNumber}</p>
                                    <p>Сумма: {amount:F2} BYN</p>
                                    <p>Дата: {transactionDate}</p>
                                 </body>
                              </html>";

            MailMessage mail = new MailMessage(senderEmail, toEmail)
            {
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            using (var client = new SmtpClient("smtp.gmail.com", 587))
            {
                client.Credentials = new NetworkCredential(senderEmail, senderPassword);

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

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
            new HomeWindow(_userId).Show();
        }

        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            var tb = sender as TextBox;
            if (tb == ContractNumberTextBox) ContractNumberTextBlock.Visibility = Visibility.Collapsed;
            else if (tb == AmountTextBox) AmountTextBlock.Visibility = Visibility.Collapsed;
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e) => UpdateHints();

        private void UpdateHints()
        {
            ContractNumberTextBlock.Visibility = string.IsNullOrWhiteSpace(ContractNumberTextBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            AmountTextBlock.Visibility = string.IsNullOrWhiteSpace(AmountTextBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ContractNumberTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.All(char.IsDigit);
        }

        private void ContractNumberTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (ContractNumberTextBox.Text.Length > 13)
            {
                ContractNumberTextBox.Text = ContractNumberTextBox.Text.Substring(0, 13);
                ContractNumberTextBox.CaretIndex = ContractNumberTextBox.Text.Length;
            }
        }

        private void AmountTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            var textBox = sender as TextBox;
            string fullText = textBox.Text.Insert(textBox.SelectionStart, e.Text);
            e.Handled = !decimal.TryParse(fullText, out _);
        }
    }
}
