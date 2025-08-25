using System.Net.Mail;
using System.Net;
using System.Windows;

namespace WpfApp1
{
    public partial class OperationsWindow : Window
    {
        private readonly DbManager _db = new DbManager();

        public OperationsWindow(int currentUserId)
        {
            InitializeComponent();
            LoadUserCards();
        }

        private async void LoadUserCards()
        {
            try
            {
                var cards = await _db.GetFullUserCardsAsync(SessionManager.CurrentUserID);
                TopUpCardComboBox.ItemsSource = cards;
                if (cards.Count > 0)
                    TopUpCardComboBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки карт: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CardToCard_Click(object sender, RoutedEventArgs e)
        {
            var cardToCardWindow = new CardToCardWindow(SessionManager.CurrentUserID);
            cardToCardWindow.Show();
            this.Close();
        }

        private void A1_Click(object sender, RoutedEventArgs e)
        {
            PhoneTopUpWindow phoneTopUpWindow = new PhoneTopUpWindow(SessionManager.CurrentUserID);
            phoneTopUpWindow.Show();
            phoneTopUpWindow.NameTextBlock.Text = "A1";
            phoneTopUpWindow.Title = "A1";
            this.Close();
        }

        private void MTS_Click(object sender, RoutedEventArgs e)
        {
            PhoneTopUpWindow phoneTopUpWindow = new PhoneTopUpWindow(SessionManager.CurrentUserID);
            phoneTopUpWindow.Show();
            phoneTopUpWindow.NameTextBlock.Text = "МТС";
            phoneTopUpWindow.Title = "МТС";
            this.Close();
        }

        private void Life_Click(object sender, RoutedEventArgs e)
        {
            PhoneTopUpWindow phoneTopUpWindow = new PhoneTopUpWindow(SessionManager.CurrentUserID);
            phoneTopUpWindow.Show();
            phoneTopUpWindow.NameTextBlock.Text = "Life:)";
            phoneTopUpWindow.Title = "Life:)";
            this.Close();
        }

        private void Byfly_Click(object sender, RoutedEventArgs e)
        {
            PaymentServicesWindow paymentServicesWindow = new PaymentServicesWindow(SessionManager.CurrentUserID);
            paymentServicesWindow.Show();
            paymentServicesWindow.NameTextBlock.Text = "ByFly";
            paymentServicesWindow.Title = "Оплата ByFly";
            this.Close();
        }

        private void Beltelecom_Click(object sender, RoutedEventArgs e)
        {
            PaymentServicesWindow paymentServicesWindow = new PaymentServicesWindow(SessionManager.CurrentUserID);
            paymentServicesWindow.Show();
            paymentServicesWindow.NameTextBlock.Text = "Beltelecom";
            paymentServicesWindow.Title = "Оплата Beltelecom";
            this.Close();
        }

        private void PayCard_Click(object sender, RoutedEventArgs e)
        {
            OrderCardWindow orderCardWindow = new OrderCardWindow(SessionManager.CurrentUserID);
            orderCardWindow.Show();
            this.Close();
        }

        private async void UpdateBalanceCard_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (TopUpCardComboBox.SelectedItem is not CardInfo selectedCard)
                {
                    MessageBox.Show("Выберите карту для пополнения.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int cardId = selectedCard.CardsID;

                decimal currentBalance = Convert.ToDecimal(await _db.ExecuteScalarAsync(
                    "SELECT Balance FROM Cards WHERE CardsID = @CardId",
                    new Dictionary<string, object> { { "@CardId", cardId } }));

                Random rand = new();
                int min = currentBalance > 5 ? 10 : 20;
                int max = currentBalance > 5 ? 300 : 1000;
                decimal topUpAmount = rand.Next(min, max + 1);

                await _db.ExecuteWithParamsAsync(
                    "UPDATE Cards SET Balance = Balance + @Amount WHERE CardsID = @CardId",
                    new Dictionary<string, object>
                    {
                { "@Amount", topUpAmount },
                { "@CardId", cardId }
                    });

                await _db.RecordTransactionAsync(new Transaction
                {
                    UserID = SessionManager.CurrentUserID,
                    TransactionType = "Автопополнение",
                    Amount = topUpAmount,
                    SenderCardID = cardId,
                    RecipientDetails = "Система автопополнения",
                    Details = $"Автоматическое пополнение карты {selectedCard.Number}",
                    Status = "Completed"
                });

                string userEmail = await _db.GetUserEmailAsync(SessionManager.CurrentUserID);
                if (!string.IsNullOrEmpty(userEmail))
                {
                    await SendBalanceTopUpReceiptEmail(userEmail, selectedCard.Number, topUpAmount);
                }

                foreach (Window window in Application.Current.Windows)
                {
                    if (window is HomeWindow homeWindow)
                    {
                        homeWindow.RefreshCards();
                        break;
                    }
                }

                var receipt = new TransactionViewModel
                {
                    CardName = selectedCard.CardName,
                    CardNumber = selectedCard.Number,
                    TransactionType = "Пополнение",
                    Amount = topUpAmount,
                    TransactionDate = DateTime.Now,
                    Status = "Completed",
                    Details = "Система автопополнения"
                };

                new ReceiptWindow(receipt).ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка пополнения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task SendBalanceTopUpReceiptEmail(string recipientEmail, string cardNumber, decimal amount)
        {
            try
            {
                string maskedCard = $"****{cardNumber[^4..]}";
                string date = DateTime.Now.ToString("dd.MM.yyyy HH:mm");

                string subject = "Чек: автоматическое пополнение баланса";

                using var mail = new MailMessage("yabank2025@gmail.com", recipientEmail)
                {
                    Subject = subject,
                    Body = $@"
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; background-color: #f4f4f4; padding: 20px; }}
        .container {{
            background-color: white; 
            padding: 20px; 
            border-radius: 8px; 
            max-width: 500px; 
            margin: auto; 
            box-shadow: 0 0 10px rgba(0,0,0,0.1);
        }}
        h2 {{
            color: #2a5885; 
            text-align: center; 
            margin-bottom: 20px;
        }}
        .detail-row {{
            margin-bottom: 15px;
            display: flex;
            justify-content: space-between;
            font-size: 16px;
        }}
        .label {{
            font-weight: bold;
            color: #555;
        }}
        hr {{
            border: none;
            border-top: 1px solid #eee;
            margin: 20px 0;
        }}
        .footer-text {{
            font-size: 12px;
            color: #888;
            text-align: center;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <h2>{subject}</h2>
        <div class='detail-row'>
            <span class='label'>Сумма пополнения:</span>
            <span>{amount:F2} BYN</span>
        </div>
        <div class='detail-row'>
            <span class='label'>Карта:</span>
            <span>{maskedCard}</span>
        </div>
        <div class='detail-row'>
            <span class='label'>Дата:</span>
            <span>{date}</span>
        </div>
        <hr />
        <p class='footer-text'>Это автоматическое письмо. Пожалуйста, не отвечайте на него.</p>
        <p class='footer-text'>&copy; {DateTime.Now.Year} YaBank</p>
    </div>
</body>
</html>",
                    IsBodyHtml = true
                };

                using var smtp = new SmtpClient("smtp.gmail.com", 587)
                {
                    Credentials = new NetworkCredential("yabank2025@gmail.com", "yruhfvsxhshgpovq"),
                    EnableSsl = true
                };

                await smtp.SendMailAsync(mail);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при отправке письма: {ex.Message}", "Ошибка почты", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
