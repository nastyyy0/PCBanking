using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WpfApp1
{
    public partial class LoginWindow : Window
    {
        private bool _inputStarted = false; // Флаг начала ввода — чтобы блокировать смену способа входа
        private bool _isInitialized = false; // Флаг инициализации окна
        private string? _sentCode; // Сохранённый отправленный код
        private readonly DbManager _db = new DbManager(); // Менеджер базы данных

        // Данные отправителя (используется для отправки писем)
        private const string senderEmail = "yabank2025@gmail.com";
        private const string senderPassword = "yruhfvsxhshgpovq";

        public LoginWindow()
        {
            InitializeComponent();

            // Изначально видим только форму с паролем
            PasswordGrid.Visibility = Visibility.Visible;
            CodeGrid.Visibility = Visibility.Collapsed;
            SendCodeButton.Visibility = Visibility.Collapsed;

            _isInitialized = true;
        }

        // Обработка переключения радио-кнопок (способ входа)
        private void Radio_Checked(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;

            // Проверяем, есть ли ввод в любом из текстбоксов
            bool anyInput =
                !string.IsNullOrWhiteSpace(EmailTextBox.Text) ||
                !string.IsNullOrWhiteSpace(PasswordTextBox.Text) ||
                !string.IsNullOrWhiteSpace(CodeTextBox.Text);

            if (anyInput)
            {
                // Если есть ввод — отменяем переключение
                if (sender == PasswordLoginRadio)
                    CodeLoginRadio.IsChecked = true;
                else if (sender == CodeLoginRadio)
                    PasswordLoginRadio.IsChecked = true;

                MessageBox.Show("Нельзя менять способ входа, пока есть ввод в полях.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Если все поля пустые — переключаемся нормально
            if (PasswordLoginRadio.IsChecked == true)
            {
                PasswordGrid.Visibility = Visibility.Visible;
                CodeGrid.Visibility = Visibility.Collapsed;
                SendCodeButton.Visibility = Visibility.Collapsed;
            }
            else if (CodeLoginRadio.IsChecked == true)
            {
                PasswordGrid.Visibility = Visibility.Collapsed;
                CodeGrid.Visibility = Visibility.Visible;

                SendCodeButton.Visibility = string.IsNullOrWhiteSpace(EmailTextBox.Text)
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }
        }

        private void InputStarted(object sender, TextChangedEventArgs e)
        {
            if (!_inputStarted)
                _inputStarted = true;

            UpdateHints();
        }

        private string _realPassword = ""; // хранит настоящий пароль

        private void PasswordTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Добавляем символ к реальному паролю
            _realPassword += e.Text;

            // Заменяем текст в текстбоксе на звёздочки
            PasswordTextBox.Text = new string('*', _realPassword.Length);
            PasswordTextBox.CaretIndex = PasswordTextBox.Text.Length;

            e.Handled = true; // предотвращаем стандартный ввод
        }

        private void PasswordTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Back && _realPassword.Length > 0)
            {
                _realPassword = _realPassword.Substring(0, _realPassword.Length - 1);
                PasswordTextBox.Text = new string('*', _realPassword.Length);
                PasswordTextBox.CaretIndex = PasswordTextBox.Text.Length;
                e.Handled = true;
            }
        }

        // Обработка изменения текста email
        private void EmailTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _inputStarted = true;
            UpdateHints();

            // Показываем кнопку "Отправить код", только если выбран режим по коду и email заполнен
            if (CodeLoginRadio.IsChecked == true && !string.IsNullOrWhiteSpace(EmailTextBox.Text))
            {
                SendCodeButton.Visibility = Visibility.Visible;
            }
            else
            {
                SendCodeButton.Visibility = Visibility.Collapsed;
            }
        }

        // Обработка нажатия кнопки "Отправить код"
        private async void SendCodeButton_Click(object sender, RoutedEventArgs e)
        {
            string email = EmailTextBox.Text.Trim();

            // Проверка существования пользователя
            if (!await _db.UserExists(email))
            {
                MessageBox.Show("Пользователь с таким email не найден.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }


            // Генерируем и сохраняем код
            _sentCode = GenerateCode();

            try
            {
                // Отправляем письмо с кодом
                await SendVerificationEmail(email, _sentCode);
                MessageBox.Show($"Код подтверждения отправлен на {email}", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при отправке письма: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Обработка кнопки "Войти"
        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string email = EmailTextBox.Text.Trim();

            // Вход по паролю
            if (PasswordLoginRadio.IsChecked == true)
            {
                // Проверка заполненности полей
                if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(PasswordTextBox.Text))
                {
                    MessageBox.Show("Пожалуйста, заполните все поля.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Проверка существования пользователя
                if (!await _db.UserExists(email))
                {
                    var res = MessageBox.Show("Пользователь не найден. Перейти к регистрации?", "Пользователь не найден",
                        MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (res == MessageBoxResult.Yes)
                    {
                        // Переход к окну регистрации
                        MainWindow mainWindow = new MainWindow();
                        mainWindow.Show();
                        this.Close();
                    }
                    return;
                }

                // Проверка пароля в базе данных
                bool passwordCorrect = await CheckPasswordAsync(email, _realPassword);
                if (passwordCorrect)
                {
                    int userId = await _db.GetUserIdByEmailAsync(email);
                    SessionManager.CurrentUserID = userId;

                    MessageBox.Show("Вход выполнен успешно!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    HomeWindow homeWindow = new HomeWindow(userId);
                    homeWindow.Show();
                    this.Close();
                }
                else
                {
                    MessageBox.Show("Неверный пароль.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            // Вход по коду из письма
            else if (CodeLoginRadio.IsChecked == true)
            {
                // Проверка заполненности полей
                if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(CodeTextBox.Text))
                {
                    MessageBox.Show("Пожалуйста, заполните все поля.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Проверка, был ли отправлен код
                if (_sentCode == null)
                {
                    MessageBox.Show("Пожалуйста, сначала отправьте код.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Сравнение введённого кода с отправленным
                if (CodeTextBox.Text.Trim() == _sentCode)
                {
                    int userId = await _db.GetUserIdByEmailAsync(email);
                    SessionManager.CurrentUserID = userId;

                    MessageBox.Show("Вход выполнен успешно!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    _sentCode = null; // Очищаем код после входа
                    HomeWindow homeWindow = new HomeWindow(userId);
                    homeWindow.Show();
                    this.Close();
                }
                else
                {
                    MessageBox.Show("Неверный код.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Проверка пароля в базе данных
        private async Task<bool> CheckPasswordAsync(string email, string password)
        {
            try
            {
                string query = "SELECT COUNT(*) FROM Users WHERE Email = @Email AND AccountPassword = @Password";
                var parameters = new Dictionary<string, object>
                {
                    {"@Email", email},
                    {"@Password", password}
                };

                int count = (int)await _db.ExecuteScalarAsync(query, parameters);
                return count > 0;
            }
            catch
            {
                return false;
            }
        }

        // Генерация 6-значного кода
        private string GenerateCode()
        {
            var rnd = new Random();
            return rnd.Next(100000, 999999).ToString();
        }

        // Отправка письма с кодом на email
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

        // Подсказки внутри текстбоксов — исчезают при фокусе
        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            var tb = sender as TextBox;
            if (tb == EmailTextBox) EmailHint.Visibility = Visibility.Collapsed;
            else if (tb == PasswordTextBox) PasswordHint.Visibility = Visibility.Collapsed;
            else if (tb == CodeTextBox) CodeHint.Visibility = Visibility.Collapsed;
        }

        // Обновление состояния подсказок при потере фокуса
        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            UpdateHints();
        }

        // Метод для отображения или скрытия подсказок
        private void UpdateHints()
        {
            EmailHint.Visibility = string.IsNullOrWhiteSpace(EmailTextBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            PasswordHint.Visibility = string.IsNullOrWhiteSpace(PasswordTextBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            CodeHint.Visibility = string.IsNullOrWhiteSpace(CodeTextBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        // Обработчик перехода на регистрацию
        private void RegisterHyperlink_Click(object sender, RoutedEventArgs e)
        {
            MainWindow registrationWindow = new MainWindow(); // Здесь должно быть окно регистрации
            registrationWindow.Show();
            this.Close();
        }

        // Возвращает HTML-содержимое письма
        private string GetEmailBodyHtml(string code)
        {
            return $@"
                <html>
                    <body style='font-family: Arial, sans-serif; background-color: #f0f2f5; padding: 30px;'>
                        <div style='max-width: 500px; margin: auto; background-color: #ffffff; padding: 30px; border-radius: 12px; box-shadow: 0 4px 12px rgba(0,0,0,0.1);'>
                            <h2 style='color: #2e6de6; margin-top: 0;'>Ваш код подтверждения</h2>
                            <p style='font-size: 16px; color: #333333;'>Пожалуйста, введите следующий код в приложении, чтобы продолжить:</p>
                            <div style='font-size: 32px; font-weight: bold; color: #2e6de6; margin: 20px 0; text-align: center;'>{code}</div>
                            <p style='font-size: 14px; color: #777777;'>Если вы не запрашивали этот код, просто проигнорируйте это сообщение.</p>
                            <hr style='margin: 30px 0; border: none; border-top: 1px solid #eeeeee;' />
                            <p style='font-size: 12px; color: #aaaaaa; text-align: center;'>Это письмо отправлено автоматически. Пожалуйста, не отвечайте на него.</p>
                        </div>
                    </body>
                </html>";
        }
    }
}
