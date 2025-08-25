using System.Net.Mail;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows;

namespace WpfApp1
{
    public partial class ForgotPasswordWindow : Window
    {
        private readonly DbManager _db = new DbManager();
        private const string senderEmail = "yabank2025@gmail.com";
        private const string senderPassword = "yruhfvsxhshgpovq";

        private readonly int _userId;

        public ForgotPasswordWindow(int userId)
        {
            InitializeComponent();
            _userId = userId;
        }

        private async void RecoverPassword_Click(object sender, RoutedEventArgs e)
        {
            string email = EmailTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(email))
            {
                MessageBox.Show("Введите адрес электронной почты.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!IsValidEmail(email))
            {
                MessageBox.Show("Введите корректный адрес электронной почты.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            RecoverPassword.IsEnabled = false;

            try
            {
                var userIdObj = await _db.ExecuteScalarAsync(
                    "SELECT UserID FROM Users WHERE Email = @Email",
                    new Dictionary<string, object> { { "@Email", email } });

                if (userIdObj == null)
                {
                    MessageBox.Show("Пользователь с таким email не найден.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                int userId = Convert.ToInt32(userIdObj);
                string newPassword = GeneratePassword();

                await _db.ExecuteWithParamsAsync(
                    "UPDATE Users SET AccountPassword = @NewPassword WHERE UserID = @UserID",
                    new Dictionary<string, object>
                    {
                        { "@NewPassword", newPassword },
                        { "@UserID", userId }
                    });

                await SendRecoveryEmail(email, newPassword);

                MessageBox.Show("Новый пароль отправлен на вашу почту.", "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при восстановлении пароля: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                RecoverPassword.IsEnabled = true;
            }
        }

        private async Task SendRecoveryEmail(string email, string newPassword)
        {
            string subject = "Восстановление пароля";
            string body = $@"
                <html>
                    <body style='font-family:Arial;'>
                        <h2 style='color:#2e6de6;'>Ваш новый пароль</h2>
                        <p>Новый пароль: <strong>{newPassword}</strong></p>
                        <p>Рекомендуем сменить его после входа в систему.</p>
                    </body>
                </html>";

            MailMessage message = new MailMessage(senderEmail, email)
            {
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            using var client = new SmtpClient("smtp.gmail.com", 587)
            {
                Credentials = new NetworkCredential(senderEmail, senderPassword),
                EnableSsl = true
            };

            try
            {
                await client.SendMailAsync(message);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при отправке письма: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GeneratePassword()
        {
            const string chars = "ABCDEFGHJKLMNOPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz0123456789";
            var rnd = new Random();
            return new string(Enumerable.Range(0, 10).Select(_ => chars[rnd.Next(chars.Length)]).ToArray());
        }

        private bool IsValidEmail(string email)
        {
            return Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
        }
    }
}
