using System.Text;
using System.Windows;
using Word = Microsoft.Office.Interop.Word;

namespace WpfApp1
{
    public partial class ReceiptWindow : Window
    {
        private readonly TransactionViewModel _transaction;

        public ReceiptWindow(TransactionViewModel transaction)
        {
            InitializeComponent();
            _transaction = transaction;
            DataContext = _transaction;
            foreach (Window window in Application.Current.Windows)
            {
                if (window is HomeWindow homeWindow)
                {
                    homeWindow.UpdateLayout();
                    break;
                }
            }
        }

        private void CreateWordButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var wordApp = new Word.Application();
                wordApp.Visible = true;
                Word.Document doc = wordApp.Documents.Add();

                var para = doc.Content.Paragraphs.Add();
                para.Range.Text = "Чек операции";
                para.Range.Font.Size = 20;
                para.Range.Font.Bold = 1;
                para.Range.InsertParagraphAfter();

                para = doc.Content.Paragraphs.Add();
                para.Range.Text = $"Имя карты: {_transaction.CardName}";
                para.Range.Font.Size = 14;
                para.Range.Font.Bold = 0;
                para.Range.InsertParagraphAfter();

                para = doc.Content.Paragraphs.Add();
                para.Range.Text = $"Номер карты: {FormatCardNumber(_transaction.CardNumber)}";
                para.Range.InsertParagraphAfter();

                para = doc.Content.Paragraphs.Add();
                para.Range.Text = $"Вид операции: {_transaction.TransactionType}";
                para.Range.InsertParagraphAfter();

                para = doc.Content.Paragraphs.Add();
                para.Range.Text = $"Сумма: {_transaction.Amount:0.00} BYN";
                para.Range.InsertParagraphAfter();

                para = doc.Content.Paragraphs.Add();
                para.Range.Text = $"Дата: {_transaction.TransactionDate:dd.MM.yyyy HH:mm}";
                para.Range.InsertParagraphAfter();

                para = doc.Content.Paragraphs.Add();
                para.Range.Text = $"Статус: {_transaction.Status}";
                para.Range.InsertParagraphAfter();

                para = doc.Content.Paragraphs.Add();
                para.Range.Text = $"Реквизиты: {_transaction.Details}";
                para.Range.InsertParagraphAfter();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Ошибка при создании Word-документа: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string FormatCardNumber(string cardNumber)
        {
            if (string.IsNullOrWhiteSpace(cardNumber))
                return string.Empty;

            string digitsOnly = new string(cardNumber.Where(char.IsDigit).ToArray());

            // Вставка пробелов каждые 4 символа
            var formatted = new StringBuilder();
            for (int i = 0; i < digitsOnly.Length; i++)
            {
                if (i > 0 && i % 4 == 0)
                    formatted.Append(" ");
                formatted.Append(digitsOnly[i]);
            }

            return formatted.ToString();
        }
    }
}
