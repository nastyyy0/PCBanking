namespace WpfApp1
{
    public class Transaction
    {
        public int UserID { get; set; }
        public string TransactionType { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int? SenderCardID { get; set; }
        public string RecipientDetails { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public string Status { get; set; } = "Completed";
    }
}
