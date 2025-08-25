using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OxyPlot;
using OxyPlot.Legends;
using OxyPlot.Series;
using OxyPlot.Axes;

namespace WpfApp1
{
    public partial class HomeWindow : Window
    {
        private bool _initialLoadDone = false;

        private readonly int currentUserId;
        private PlotModel _currencyModel;

        private readonly int _userId;

        public HomeWindow(int userId)
        {
            InitializeComponent();
            _userId = userId;
            currentUserId = userId;

            Loaded += HomeWindow_Loaded;
            Activated += async (s, e) =>
            {
                if (_initialLoadDone)
                    await RefreshHomeDataAsync();
            };
        }
        public void LoadUserCards()
        {
            _ = RefreshHomeDataAsync(); // без await, потому что это void
        }

        public void RefreshCards()
        {
            // Повторно загрузи список карт
            RefreshHomeDataAsync(); // или аналогичная функция
        }

        private async void HomeWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var db = new DbManager();
            var cards = await db.GetFullUserCardsAsync(currentUserId);

            // Добавляем элемент "Добавить ещё карту"
            cards.Add(new CardInfo
            {
                CardName = "➕ Добавить ещё карту",
                CardsID = -1 // специальный ID, чтобы отличить
            });

            PaymentCardComboBox.ItemsSource = cards;

            if (cards.Count > 1)
            {
                PaymentCardComboBox.Visibility = Visibility.Visible;
                PaymentCardComboBox.SelectedIndex = 0;
                AddCardButton.Visibility = Visibility.Collapsed;
            }
            else
            {
                PaymentCardComboBox.Visibility = Visibility.Collapsed;
                ShowAddCardPlaceholder();
            }

            await LoadSpendingChartAsync();
            LoadCurrencyChart();

            FromCurrencyComboBox.SelectedIndex = 0;
            ToCurrencyComboBox.SelectedIndex = 1;
        }



        private async Task RefreshHomeDataAsync()
        {
            var db = new DbManager();
            var cards = await db.GetFullUserCardsAsync(currentUserId);

            // Добавить "Добавить ещё карту"
            if (!cards.Exists(c => c.CardsID == -1))
            {
                cards.Add(new CardInfo
                {
                    CardName = "➕ Добавить ещё карту",
                    CardsID = -1
                });
            }

            PaymentCardComboBox.ItemsSource = null;
            PaymentCardComboBox.ItemsSource = cards;

            if (cards.Count > 1)
            {
                PaymentCardComboBox.Visibility = Visibility.Visible;
                PaymentCardComboBox.SelectedIndex = 0;
                AddCardButton.Visibility = Visibility.Collapsed;
            }
            else
            {
                PaymentCardComboBox.Visibility = Visibility.Collapsed;
                ShowAddCardPlaceholder();
            }

            await LoadSpendingChartAsync(); // Перерисовка круговой диаграммы
            LoadCurrencyChart();            // Обновление курса
        }

        private void PaymentCardComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PaymentCardComboBox.SelectedItem is CardInfo card)
            {
                if (card.CardsID == -1)
                {
                    // Открываем окно добавления карты
                    AddCardWindow addCardWindow = new AddCardWindow(currentUserId);
                    addCardWindow.ShowDialog();

                    // После закрытия — перезагружаем список карт
                    HomeWindow_Loaded(this, null);
                    return;
                }

                // Случайный фон
                var rnd = new Random();
                CardBackground.Fill = _cardColors[rnd.Next(_cardColors.Count)];

                // Отображение информации
                CardNameText.Text = card.CardName;
                CardNumberText.Text = FormatCardNumber(card.Number);
                CardHolderText.Text = card.Cardholder;
                CardBalanceText.Text = card.Balance + "BYN";
            }
        }

        private void LoadCurrencyChart()
        {
            var dates = new List<string> { "Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс" };
            var rates = new List<double> { 3.18, 3.20, 3.21, 3.22, 3.19, 3.23, 3.25 };

            _currencyModel = new PlotModel
            {
                Title = "Курс USD/BYN за неделю",
                TextColor = OxyColors.White,
                PlotAreaBorderColor = OxyColors.White
            };

            var categoryAxis = new CategoryAxis
            {
                Position = AxisPosition.Bottom,
                ItemsSource = dates,
                TextColor = OxyColors.White,
                AxislineColor = OxyColors.White,
                MajorGridlineColor = OxyColors.Gray,
                MinorGridlineColor = OxyColors.LightGray
            };

            var valueAxis = new LinearAxis
            {
                Position = AxisPosition.Left,
                MinimumPadding = 0.1,
                MaximumPadding = 0.1,
                TextColor = OxyColors.White,
                AxislineColor = OxyColors.White,
                MajorGridlineColor = OxyColors.Gray,
                MinorGridlineColor = OxyColors.LightGray
            };

            var purpleColor = OxyColor.Parse("#FF8892E6");

            var series = new LineSeries
            {
                Title = "USD/BYN",
                Color = purpleColor,
                MarkerType = MarkerType.Circle,
                MarkerSize = 2,
                MarkerStroke = OxyColors.White,
                MarkerFill = purpleColor
            };

            for (int i = 0; i < rates.Count; i++)
            {
                series.Points.Add(new DataPoint(i, rates[i]));
            }

            _currencyModel.Axes.Add(categoryAxis);
            _currencyModel.Axes.Add(valueAxis);
            _currencyModel.Series.Add(series);

            CurrencyChartView.Model = _currencyModel;
        }

        private void ConvertCurrencyButton_Click(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(AmountTextBox.Text, out double amount))
            {
                ResultTextBlock.Text = "Введите корректную сумму.";
                return;
            }

            string from = (FromCurrencyComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
            string to = (ToCurrencyComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();

            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to))
            {
                ResultTextBlock.Text = "Выберите валюты.";
                return;
            }

            double rate = GetExchangeRate(from, to);

            if (rate <= 0)
            {
                ResultTextBlock.Text = $"Нет данных для пары {from}/{to}.";
                return;
            }

            double result = amount * rate;
            ResultTextBlock.Text = $"{amount} {from} = {result:F2} {to}";
        }

        private double GetExchangeRate(string from, string to)
        {
            // Курсы относительно USD
            var rates = new Dictionary<string, double>
    {
        { "USD", 1.0 },
        { "BYN", 3.2 },
        { "EUR", 0.92 },
        { "RUB", 88.5 }
    };

            if (!rates.ContainsKey(from) || !rates.ContainsKey(to))
                return 0;

            // Перевод из from → USD → to
            double usdValue = 1 / rates[from];
            double resultRate = usdValue * rates[to];

            return resultRate;
        }

        private void ShowAddCardPlaceholder()
        {
            CardBackground.Fill = Brushes.LightGray;
            CardNameText.Text = "";
            CardNumberText.Text = "";
            CardHolderText.Text = "";
            AddCardButton.Visibility = Visibility.Visible;
        }

        private void AddCardButton_Click(object sender, RoutedEventArgs e)
        {
            AddCardWindow addCardWindow = new AddCardWindow(currentUserId);
            addCardWindow.Show();
            HomeWindow_Loaded(this, null); 
        }

        private async void CardsButton_Click(object sender, RoutedEventArgs e)
        {
            var db = new DbManager();
            bool hasCards = await db.UserHasCardsAsync(currentUserId);

            if (hasCards)
            {
                CardsWindow cardsWindow = new CardsWindow(currentUserId);
                cardsWindow.Show();
            }
            else
            {
                MessageBox.Show("У вас ещё нет добавленных карт.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void OperationButton_Click(object sender, RoutedEventArgs e)
        {
            OperationsWindow operationsWindow = new OperationsWindow(currentUserId);
            operationsWindow.Show();
        }

        private void HistoryButton_Click(object sender, RoutedEventArgs e)
        {
            HistoryWindow historyWindow = new HistoryWindow(currentUserId);
            historyWindow.Show();
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            // Пример: если есть окно настроек — также можно передать ID
            SettingsWindow settingsWindow = new SettingsWindow(currentUserId);
            settingsWindow.Show();
        }

        private readonly List<Brush> _cardColors = new()
        {
    Brushes.SteelBlue,
    Brushes.MediumPurple,
    Brushes.IndianRed,
    Brushes.Teal,
    Brushes.DarkSlateBlue,
    Brushes.ForestGreen
        };
        internal readonly int userId;

        private string FormatCardNumber(string number)
        {
            if (number.Length == 16)
                return string.Join(" ", Enumerable.Range(0, 4).Select(i => number.Substring(i * 4, 4)));
            return number;
        }

        private async Task LoadSpendingChartAsync()
        {
            var db = new DbManager();

            bool hasCards = await db.UserHasCardsAsync(currentUserId);

            if (!hasCards)
            {
                PieChartView.Visibility = Visibility.Collapsed;
                NoTransactionsPanel.Visibility = Visibility.Visible;
                return;
            }

            var stats = await db.GetSpendingGroupedByTypeAsync(currentUserId);

            // Исключаем "Автопополнение"
            var filteredStats = stats
                .Where(kv => !string.Equals(kv.Key, "Автопополнение", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            if (filteredStats == null || filteredStats.Count == 0)
            {
                PieChartView.Visibility = Visibility.Collapsed;
                NoTransactionsPanel.Visibility = Visibility.Visible;
                return;
            }

            NoTransactionsPanel.Visibility = Visibility.Collapsed;
            PieChartView.Visibility = Visibility.Visible;

            var model = new PlotModel
            {
                IsLegendVisible = true
            };

            var legend = new Legend
            {
                LegendPlacement = LegendPlacement.Outside,
                LegendPosition = LegendPosition.RightTop,
                LegendOrientation = LegendOrientation.Vertical,
                LegendBorderThickness = 0
            };
            model.Legends.Add(legend);

            var pieSeries = new PieSeries
            {
                StrokeThickness = 0.5,
                AngleSpan = 360,
                StartAngle = 0,
                InsideLabelPosition = 0.8,
                InsideLabelFormat = "{2:P0}",  // проценты
                OutsideLabelFormat = ""        // вне не показываем
            };

            foreach (var kv in filteredStats)
            {
                pieSeries.Slices.Add(new PieSlice($"{kv.Key}: {kv.Value:F2} BYN", (double)kv.Value));
            }

            model.Series.Add(pieSeries);

            PieChartView.Model = model;
        }


        private void AddTransactionButton_Click(object sender, RoutedEventArgs e)
        {
            // Открываем окно операций с передачей currentUserId
            OperationsWindow operationsWindow = new OperationsWindow(currentUserId);
            operationsWindow.Show();
        }

        private void PromoButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Скоро вы сможете оформить премиальный счёт с бонусами!",
                            "Премиум-услуги",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
        }

        // Подсказки внутри текстбоксов — исчезают при фокусе
        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            var tb = sender as TextBox;
            if (tb == AmountTextBox) AmountPlaceholder.Visibility = Visibility.Collapsed;
        }

        // Обновление состояния подсказок при потере фокуса
        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            UpdateHints();
        }

        // Метод для отображения или скрытия подсказок
        private void UpdateHints()
        {
            AmountPlaceholder.Visibility = string.IsNullOrWhiteSpace(AmountTextBox.Text) ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
