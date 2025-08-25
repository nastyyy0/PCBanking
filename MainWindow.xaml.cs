using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WpfApp1
{
    public partial class MainWindow : Window
    {
        private readonly DbManager _db = new DbManager();
        private string verificationCode = string.Empty;

        private const string senderEmail = "yabank2025@gmail.com";
        private const string senderPassword = "yruhfvsxhshgpovq";

        // Реальные значения паролей
        private string _accountPassword = "";
        private string _repeatAccountPassword = "";

        public MainWindow()
        {
            InitializeComponent();

            VerificationCodeGrid.Visibility = Visibility.Collapsed;
            ConfirmButton.Visibility = Visibility.Collapsed;

            RegisterButton.Click += RegisterButton_Click;
            ConfirmButton.Click += ConfirmButton_Click;

            EmailTextBox.GotFocus += (s, e) => EmailTextBlock.Visibility = Visibility.Collapsed;
            EmailTextBox.LostFocus += (s, e) => { if (string.IsNullOrWhiteSpace(EmailTextBox.Text)) EmailTextBlock.Visibility = Visibility.Visible; };

            FullNameTextBox.GotFocus += (s, e) => FullNameTextBlock.Visibility = Visibility.Collapsed;
            FullNameTextBox.LostFocus += (s, e) => { if (string.IsNullOrWhiteSpace(FullNameTextBox.Text)) FullNameTextBlock.Visibility = Visibility.Visible; };

            AccountPasswordTextBox.GotFocus += (s, e) => AccountPasswordTextBlock.Visibility = Visibility.Collapsed;
            AccountPasswordTextBox.LostFocus += (s, e) => { if (string.IsNullOrWhiteSpace(AccountPasswordTextBox.Text)) AccountPasswordTextBlock.Visibility = Visibility.Visible; };

            RepeatAccountPasswordTextBox.GotFocus += (s, e) => RepeatAccountPasswordTextBlock.Visibility = Visibility.Collapsed;
            RepeatAccountPasswordTextBox.LostFocus += (s, e) => { if (string.IsNullOrWhiteSpace(RepeatAccountPasswordTextBox.Text)) RepeatAccountPasswordTextBlock.Visibility = Visibility.Visible; };

            VerificationCodeTextBox.GotFocus += (s, e) => VerificationCodeTextBlock.Visibility = Visibility.Collapsed;
            VerificationCodeTextBox.LostFocus += (s, e) => { if (string.IsNullOrWhiteSpace(VerificationCodeTextBox.Text)) VerificationCodeTextBlock.Visibility = Visibility.Visible; };

            // Подключение обработчиков для маскировки
            AccountPasswordTextBox.PreviewTextInput += AccountPasswordTextBox_PreviewTextInput;
            AccountPasswordTextBox.PreviewKeyDown += AccountPasswordTextBox_PreviewKeyDown;

            RepeatAccountPasswordTextBox.PreviewTextInput += RepeatAccountPasswordTextBox_PreviewTextInput;
            RepeatAccountPasswordTextBox.PreviewKeyDown += RepeatAccountPasswordTextBox_PreviewKeyDown;
        }

        // Маскировка основного пароля
        private void AccountPasswordTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            _accountPassword += e.Text;
            AccountPasswordTextBox.Text = new string('*', _accountPassword.Length);
            AccountPasswordTextBox.CaretIndex = AccountPasswordTextBox.Text.Length;
            e.Handled = true;
        }

        private void AccountPasswordTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Back && _accountPassword.Length > 0)
            {
                _accountPassword = _accountPassword.Substring(0, _accountPassword.Length - 1);
                AccountPasswordTextBox.Text = new string('*', _accountPassword.Length);
                AccountPasswordTextBox.CaretIndex = AccountPasswordTextBox.Text.Length;
                e.Handled = true;
            }
        }

        // Маскировка повтора пароля
        private void RepeatAccountPasswordTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            _repeatAccountPassword += e.Text;
            RepeatAccountPasswordTextBox.Text = new string('*', _repeatAccountPassword.Length);
            RepeatAccountPasswordTextBox.CaretIndex = RepeatAccountPasswordTextBox.Text.Length;
            e.Handled = true;
        }

        private void RepeatAccountPasswordTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Back && _repeatAccountPassword.Length > 0)
            {
                _repeatAccountPassword = _repeatAccountPassword.Substring(0, _repeatAccountPassword.Length - 1);
                RepeatAccountPasswordTextBox.Text = new string('*', _repeatAccountPassword.Length);
                RepeatAccountPasswordTextBox.CaretIndex = RepeatAccountPasswordTextBox.Text.Length;
                e.Handled = true;
            }
        }

        private async void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            string email = EmailTextBox.Text.Trim();

            if (await _db.UserExists(email))
            {
                MessageBox.Show("Пользователь с таким email уже зарегистрирован.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!ValidateInputs())
            {
                MessageBox.Show("Проверьте правильность заполнения всех полей.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            verificationCode = GenerateVerificationCode();

            try
            {
                await SendVerificationEmail(email, verificationCode);
                VerificationCodeGrid.Visibility = Visibility.Visible;
                ConfirmButton.Visibility = Visibility.Visible;

                MessageBox.Show("На указанный email отправлен код подтверждения.", "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при отправке письма: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            if (VerificationCodeTextBox.Text.Trim() == verificationCode)
            {
                try
                {
                    string email = EmailTextBox.Text.Trim();
                    string fullName = FullNameTextBox.Text.Trim();

                    var parameters = new Dictionary<string, object>
                    {
                        { "@Email", email },
                        { "@FullName", fullName },
                        { "@AccountPassword", _accountPassword }
                    };

                    string insertQuery = @"INSERT INTO Users (Email, FullName, AccountPassword)
                                           VALUES (@Email, @FullName, @AccountPassword)";

                    bool success = await _db.ExecuteNonQueryAsync(insertQuery, parameters);

                    if (!success)
                    {
                        MessageBox.Show("Не удалось сохранить пользователя.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    int userId = await _db.GetUserIdByEmailAsync(email);
                    SessionManager.CurrentUserID = userId;

                    MessageBox.Show("Регистрация успешно завершена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    new HomeWindow(userId).Show();
                    this.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при сохранении пользователя: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Неверный код подтверждения.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool ValidateInputs()
        {
            if (string.IsNullOrWhiteSpace(EmailTextBox.Text) || !IsValidEmail(EmailTextBox.Text))
            {
                MessageBox.Show("Введите корректный email.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (string.IsNullOrWhiteSpace(FullNameTextBox.Text))
            {
                MessageBox.Show("Введите ФИО.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (string.IsNullOrWhiteSpace(_accountPassword))
            {
                MessageBox.Show("Введите пароль.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (_accountPassword != _repeatAccountPassword)
            {
                MessageBox.Show("Пароли не совпадают.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (_accountPassword.Length < 6 || !Regex.IsMatch(_accountPassword, @"[A-Za-z]") || !Regex.IsMatch(_accountPassword, @"[0-9]"))
            {
                MessageBox.Show("Пароль должен быть не менее 6 символов и содержать буквы и цифры.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            return true;
        }

        private bool IsValidEmail(string email)
        {
            var pattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
            return Regex.IsMatch(email, pattern);
        }

        private string GenerateVerificationCode()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var rand = new Random();
            var sb = new StringBuilder();
            for (int i = 0; i < 4; i++) sb.Append(chars[rand.Next(chars.Length)]);
            return sb.ToString();
        }

        private async Task SendVerificationEmail(string email, string code)
        {
            MailMessage mail = new MailMessage(senderEmail, email)
            {
                Subject = "Код подтверждения",
                Body = GetEmailBodyHtml(code),
                IsBodyHtml = true
            };

            using (var client = new SmtpClient("smtp.gmail.com", 587))
            {
                client.Credentials = new NetworkCredential(senderEmail, senderPassword);
                client.EnableSsl = true;
                await client.SendMailAsync(mail);
            }
        }

        private void LoginHyperlink_Click(object sender, RoutedEventArgs e)
        {
            new LoginWindow().Show();
            this.Close();
        }

        private string GetEmailBodyHtml(string code)
        {
            return $@"
                <html>
                    <body style='font-family: Arial; background-color: #f4f4f4; padding: 20px;'>
                        <div style='background-color: #fff; padding: 20px; border-radius: 10px; box-shadow: 0 2px 5px rgba(0,0,0,0.1);'>
                            <h2 style='color: #4a90e2;'>Код подтверждения</h2>
                            <p>Введите этот код в приложении:</p>
                            <h1>{code}</h1>
                            <p style='font-size: 14px; color: #777777;'>Если вы не запрашивали этот код, просто проигнорируйте это сообщение.</p>
                        </div>
                    </body>
                </html>";
        }
    }
}
