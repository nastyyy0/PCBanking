using System.Windows;

namespace WpfApp1
{
    public partial class SettingsWindow : Window
    {
        private readonly int currentUserId;
        private readonly int _userId;

        public SettingsWindow()
        {
            InitializeComponent();
        }
        public SettingsWindow(int userId)
        {
            InitializeComponent();
            _userId = userId;
            currentUserId = userId;
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            // Открываем окно входа
            var loginWindow = new LoginWindow();
            loginWindow.Show();

            // Закрываем все остальные окна
            foreach (Window window in Application.Current.Windows)
            {
                if (window != loginWindow)
                {
                    window.Close();
                }
            }

            this.Close();
        }

        private void ForgotPassword_Click(object sender, RoutedEventArgs e)
        {
            var forgotWindow = new ForgotPasswordWindow(_userId);
            forgotWindow.ShowDialog();

            this.Close();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}

