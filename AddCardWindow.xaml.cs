using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WpfApp1
{
    public partial class AddCardWindow : Window
    {
        private readonly DbManager _db = new DbManager();
        private readonly int _userId;

        private readonly string _presetNumber;
        private readonly DateTime _presetExpiry;
        private readonly string _presetCVV;
        private readonly string _presetCardholder;

        public AddCardWindow() : this(0) { }

        public AddCardWindow(int userId)
        {
            InitializeComponent();
            _userId = userId;
        }

        // Конструктор с заполненными полями (карта уже создана, пользователь должен только ввести имя)
        public AddCardWindow(int userId, string number, DateTime expiry, string cvv, string cardholder)
            : this(userId)
        {
            _presetNumber = number;
            _presetExpiry = expiry;
            _presetCVV = cvv;
            _presetCardholder = cardholder;

            // Заполняем поля
            CardNumberTextBox.Text = FormatCardNumber(number);
            ExpiryTextBox.Text = expiry.ToString("MM/yyyy");
            CVVTextBox.Text = cvv;
            CardholderTextBox.Text = cardholder;

            // Блокируем редактирование этих полей
            CardNumberTextBox.IsEnabled = false;
            ExpiryTextBox.IsEnabled = false;
            CVVTextBox.IsEnabled = false;
            CardholderTextBox.IsEnabled = false;
        }

        private void AddCardButton_Click(object sender, RoutedEventArgs e)
        {

            if (string.IsNullOrWhiteSpace(CardNameTextBox?.Text))
            {
                MessageBox.Show("Введите имя карты", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string cardNumber = (_presetNumber ?? CardNumberTextBox.Text.Replace(" ", "")).Trim();
            string cardholder = (_presetCardholder ?? CardholderTextBox.Text.Trim());
            string cvv = (_presetCVV ?? CVVTextBox.Text.Trim());
            DateTime expiry;

            // Проверка CVV
            if (cvv.Length != 3 || !cvv.All(char.IsDigit))
            {
                MessageBox.Show("CVV должен содержать ровно 3 цифры", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Обработка даты
            if (!DateTime.TryParseExact(ExpiryTextBox.Text, "MM/yyyy", CultureInfo.InvariantCulture,
                                        DateTimeStyles.None, out expiry))
            {
                MessageBox.Show("Дата должна быть в формате MM/yyyy", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            expiry = new DateTime(expiry.Year, expiry.Month, DateTime.DaysInMonth(expiry.Year, expiry.Month));
            if (expiry < DateTime.Now.Date)
            {
                MessageBox.Show("Срок действия не может быть в прошлом", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Генерация случайного баланса (200-1000)
                Random rnd = new Random();
                decimal balance = rnd.Next(200, 1001);

                string insertQuery = @"
                            INSERT INTO Cards (Number, CardName, Cardholder, Duration, CVV, Balance)
                            VALUES (@Number, @CardName, @Cardholder, @Duration, @CVV, @Balance);
                            SELECT SCOPE_IDENTITY();";

                var parameters = new Dictionary<string, object>
                {
                    { "@Number", cardNumber },
                    { "@CardName", CardNameTextBox.Text.Trim() },
                    { "@Cardholder", cardholder },
                    { "@Duration", expiry },
                    { "@CVV", cvv },
                    { "@Balance", balance }
                };

                int cardId = Convert.ToInt32(_db.ExecuteScalar(insertQuery, parameters));

                string userCardQuery = "INSERT INTO UserCards (UserID, CardsID) VALUES (@UserID, @CardsID)";
                _db.ActionWithParams(userCardQuery, new Dictionary<string, object>
                {
                    { "@UserID", _userId },
                    { "@CardsID", cardId }
                });

                MessageBox.Show($"Карта успешно добавлена. Баланс: {balance:F2} BYN", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                foreach (Window window in Application.Current.Windows)
                {
                    if (window is HomeWindow home && home.userId == _userId)
                    {
                        home.LoadUserCards(); 
                        break;
                    }
                }

                // Обновляем главное окно — ищем открытое и вызываем метод обновления
                foreach (Window win in Application.Current.Windows)
                {
                    if (win is HomeWindow home)
                    {
                        home.RefreshCards(); 
                        break;
                    }
                }


                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при добавлении карты: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                if (tb == CardNumberTextBox) CardNumberHint.Visibility = Visibility.Collapsed;
                else if (tb == CardNameTextBox) CardNameHint.Visibility = Visibility.Collapsed;
                else if (tb == CardholderTextBox) CardholderHint.Visibility = Visibility.Collapsed;
                else if (tb == ExpiryTextBox) ExpiryHint.Visibility = Visibility.Collapsed;
                else if (tb == CVVTextBox) CVVHint.Visibility = Visibility.Collapsed;
            }
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e) => UpdateHints();

        private void UpdateHints()
        {
            CardNumberHint.Visibility = string.IsNullOrWhiteSpace(CardNumberTextBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            CardNameHint.Visibility = string.IsNullOrWhiteSpace(CardNameTextBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            CardholderHint.Visibility = string.IsNullOrWhiteSpace(CardholderTextBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            ExpiryHint.Visibility = string.IsNullOrWhiteSpace(ExpiryTextBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            CVVHint.Visibility = string.IsNullOrWhiteSpace(CVVTextBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ExpiryTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !char.IsDigit(e.Text, 0);
        }

        private void ExpiryTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                string digits = tb.Text.Replace("/", "");
                if (digits.Length > 2) digits = digits.Insert(2, "/");
                tb.Text = digits;
                tb.CaretIndex = tb.Text.Length;
            }
        }

        private void CardNumberTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                string text = tb.Text.Replace(" ", "");
                if (text.Length > 16) text = text.Substring(0, 16);

                var formatted = new StringBuilder();
                for (int i = 0; i < text.Length; i++)
                {
                    if (i > 0 && i % 4 == 0) formatted.Append(' ');
                    formatted.Append(text[i]);
                }

                tb.Text = formatted.ToString();
                tb.CaretIndex = tb.Text.Length;  // курсор в конец
            }
        }

        private string FormatCardNumber(string plain) =>
            string.Join(" ", Enumerable.Range(0, 4).Select(i => plain.Substring(i * 4, 4)));
    }
}
