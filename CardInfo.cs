namespace WpfApp1
{
    public class CardInfo
    {
        public int CardsID { get; set; }
        public string CardName { get; set; } = string.Empty;
        public string Number { get; set; } = string.Empty;
        public string Cardholder { get; set; } = string.Empty;
        public DateTime Duration { get; set; }
        public string CVV { get; set; } = string.Empty; 
        public decimal Balance { get; set; }
        public int UserID { get; set; } // поле для связи с пользователем

        public string Expiry => Duration.ToString("MM/yyyy");

        // Используется для отображения в ComboBox
        public string DisplayText => $"{CardName} ({Number}) — {Balance:F2} BYN";

        public override string ToString()
        {
            return DisplayText;
        }
    }
}
