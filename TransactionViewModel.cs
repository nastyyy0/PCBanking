namespace WpfApp1
{
    public class TransactionViewModel
    {
        public string CardName { get; set; }
        public string TransactionType { get; set; } 
        public string CardNumber { get; set; }
        public string Details { get; set; }
        public decimal Amount { get; set; }
        public DateTime TransactionDate { get; set; }
        public string Status { get; set; }
    }
}
