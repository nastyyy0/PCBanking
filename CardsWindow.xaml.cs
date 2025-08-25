using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace WpfApp1
{
    public partial class CardsWindow : Window
    {
        private int _userId = 1;
        private DbManager _db = new DbManager();

        private GridViewColumnHeader _lastHeaderClicked;
        private ListSortDirection _lastDirection = ListSortDirection.Ascending;

        private List<CardInfo> _allCards = new(); // Добавлено

        public CardsWindow()
        {
            InitializeComponent();
        }

        public CardsWindow(int userId)
        {
            InitializeComponent();
            _userId = userId;
            LoadUserCards();
        }

        private async void LoadUserCards()
        {
            CardsListView.ItemsSource = _allCards;

            _allCards = await _db.GetFullUserCardsAsync(_userId);
            CardsListView.ItemsSource = _allCards;


            if (_allCards.Count == 0)
            {
                MessageBox.Show("Еще нет карт", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Search_txb_TextChanged(object sender, TextChangedEventArgs e)
        {
            string search = Search_txb.Text.Trim().ToLower();

            var filtered = _allCards.Where(card =>
                (card.CardName?.ToLower().Contains(search) ?? false) ||
                (card.Cardholder?.ToLower().Contains(search) ?? false) ||
                (card.Number?.ToLower().Contains(search) ?? false) ||
                card.CVV.Contains(search) ||
                card.Duration.ToString("MM/yyyy").Contains(search) ||
                card.Balance.ToString().Contains(search)
            ).ToList();

            CardsListView.ItemsSource = filtered;
        }

        private void CardsListView_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is not DependencyObject source) return;

            // Поиск заголовка столбца
            var headerClicked = VisualUpwardSearch<GridViewColumnHeader>(source);

            if (headerClicked?.Column == null || headerClicked.Role == GridViewColumnHeaderRole.Padding)
                return;

            string sortBy = GetBindingPropertyName(headerClicked.Column.DisplayMemberBinding);

            if (string.IsNullOrEmpty(sortBy))
                return;

            ListSortDirection direction;
            if (headerClicked == _lastHeaderClicked && _lastDirection == ListSortDirection.Ascending)
            {
                direction = ListSortDirection.Descending;
            }
            else
            {
                direction = ListSortDirection.Ascending;
            }

            Sort(sortBy, direction);

            _lastHeaderClicked = headerClicked;
            _lastDirection = direction;
        }

        private void Sort(string sortBy, ListSortDirection direction)
        {
            ICollectionView dataView = CollectionViewSource.GetDefaultView(CardsListView.ItemsSource);

            dataView.SortDescriptions.Clear();
            dataView.SortDescriptions.Add(new SortDescription(sortBy, direction));
            dataView.Refresh();
        }

        private string GetBindingPropertyName(BindingBase bindingBase)
        {
            if (bindingBase is Binding binding)
            {
                return binding.Path.Path;
            }
            return string.Empty;
        }

        private T? VisualUpwardSearch<T>(DependencyObject source) where T : DependencyObject
        {
            while (source != null && source is not T)
            {
                source = VisualTreeHelper.GetParent(source);
            }
            return source as T;
        }

        private async void CardsListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (CardsListView.SelectedItem is not CardInfo selectedCard)
                return;

            MessageBoxResult result = MessageBox.Show(
                $"Вы хотите изменить имя карты \"{selectedCard.CardName}\"?",
                "Подтверждение",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (result == MessageBoxResult.Yes)
            {
                string? newName = PromptForCardName(selectedCard.CardName);

                if (!string.IsNullOrWhiteSpace(newName) && newName != selectedCard.CardName)
                {
                    var parameters = new Dictionary<string, object>
            {
                { "@NewName", newName },
                { "@CardID", selectedCard.CardsID }
            };

                    await _db.ExecuteWithParamsAsync(
                        "UPDATE Cards SET CardName = @NewName WHERE CardsID = @CardID",
                        parameters
                    );

                    selectedCard.CardName = newName;
                    CardsListView.Items.Refresh(); // Обновить отображение

                    // Найти открытое окно HomeWindow и обновить в нём данные
                    foreach (Window window in Application.Current.Windows)
                    {
                        if (window is HomeWindow homeWindow)
                        {
                            homeWindow.RefreshCards();
                            break;
                        }
                    }
                }
            }
        }

        private string? PromptForCardName(string currentName)
        {
            var inputDialog = new Window
            {
                Title = "Новое имя карты",
                Width = 300,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow
            };

            var stack = new StackPanel { Margin = new Thickness(10) };

            var label = new TextBlock { Text = "Введите новое имя карты:", Margin = new Thickness(0, 0, 0, 5) };
            var textBox = new TextBox { Text = currentName, Margin = new Thickness(0, 0, 0, 10) };
            var okButton = new Button { Content = "ОК", IsDefault = true, Width = 60, HorizontalAlignment = HorizontalAlignment.Right };

            okButton.Click += (_, _) => inputDialog.DialogResult = true;

            stack.Children.Add(label);
            stack.Children.Add(textBox);
            stack.Children.Add(okButton);

            inputDialog.Content = stack;

            return inputDialog.ShowDialog() == true ? textBox.Text.Trim() : null;
        }
    }
}
