using ClosedXML.Excel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace WpfApp1
{
    public partial class HistoryWindow : Window
    {
        private readonly DbManager _db = new DbManager();
        private readonly int _currentUserId;

        private GridViewColumnHeader _lastHeaderClicked;
        private ListSortDirection _lastDirection = ListSortDirection.Ascending;

        private List<TransactionViewModel> _allTransactions = new(); // Храним все транзакции

        public HistoryWindow(int currentUserId)
        {
            InitializeComponent();
            _currentUserId = currentUserId;

            HistoryListView.AddHandler(GridViewColumnHeader.ClickEvent, new RoutedEventHandler(GridViewColumnHeader_Click));
            LoadTransactionsAsync();
        }

        private async void LoadTransactionsAsync()
        {
            try
            {
                var transactions = await _db.GetUserTransactionsAsync(_currentUserId);
                _allTransactions = transactions; // сохраняем все
                HistoryListView.ItemsSource = _allTransactions;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки транзакций: {ex.Message}");
            }
        }

        private void HistoryListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (HistoryListView.SelectedItem is TransactionViewModel selectedTransaction)
            {
                var receiptWindow = new ReceiptWindow(selectedTransaction);
                receiptWindow.ShowDialog();
            }
        }

        private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is not GridViewColumnHeader header || header.Column == null || header.Column.DisplayMemberBinding == null)
                return;

            string sortBy = (header.Column.DisplayMemberBinding as Binding)?.Path?.Path;
            if (string.IsNullOrEmpty(sortBy)) return;

            ListSortDirection direction = ListSortDirection.Ascending;

            if (header == _lastHeaderClicked)
                direction = _lastDirection == ListSortDirection.Ascending ? ListSortDirection.Descending : ListSortDirection.Ascending;

            Sort(sortBy, direction);

            _lastHeaderClicked = header;
            _lastDirection = direction;
        }

        private void Sort(string sortBy, ListSortDirection direction)
        {
            ICollectionView dataView = CollectionViewSource.GetDefaultView(HistoryListView.ItemsSource);

            dataView.SortDescriptions.Clear();
            dataView.SortDescriptions.Add(new SortDescription(sortBy, direction));
            dataView.Refresh();
        }

        private void Search_txb_TextChanged(object sender, TextChangedEventArgs e)
        {
            string search = Search_txb.Text.Trim().ToLower();

            var filtered = _allTransactions.Where(tr =>
                (tr.CardName?.ToLower().Contains(search) ?? false) ||
                (tr.TransactionType?.ToLower().Contains(search) ?? false) ||
                (tr.Status?.ToLower().Contains(search) ?? false) ||
                tr.Amount.ToString().Contains(search) ||
                tr.TransactionDate.ToString("dd.MM.yyyy HH:mm").Contains(search)
            ).ToList();

            HistoryListView.ItemsSource = filtered;
        }

        private void ExportToExcel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Создаем временный файл в системной папке
                string tempFile = Path.Combine(Path.GetTempPath(), $"История_операций_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");

                using (var workbook = new ClosedXML.Excel.XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("История операций");

                    // Заголовки
                    worksheet.Cell(1, 1).Value = "Имя карты";
                    worksheet.Cell(1, 2).Value = "Вид операции";
                    worksheet.Cell(1, 3).Value = "Сумма (BYN)";
                    worksheet.Cell(1, 4).Value = "Дата";
                    worksheet.Cell(1, 5).Value = "Статус";

                    // Данные
                    for (int i = 0; i < _allTransactions.Count; i++)
                    {
                        var tr = _allTransactions[i];
                        worksheet.Cell(i + 2, 1).Value = tr.CardName;
                        worksheet.Cell(i + 2, 2).Value = tr.TransactionType;
                        worksheet.Cell(i + 2, 3).Value = tr.Amount;
                        worksheet.Cell(i + 2, 4).Value = tr.TransactionDate.ToString("dd.MM.yyyy HH:mm");
                        worksheet.Cell(i + 2, 5).Value = tr.Status;
                    }

                    workbook.SaveAs(tempFile); // сохраняем во временный файл
                }

                // Открываем файл
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo()
                {
                    FileName = tempFile,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при экспорте:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
