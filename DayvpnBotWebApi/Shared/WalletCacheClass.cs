namespace DayvpnBotWebApi.Shared
{
    public class WalletCacheClass
    {
        public int? TransactionRequestId { get; set; }
        public PaymentMethod? PaymentMethod { get; set; }
        public decimal RequestBalance { get; set; } = 0;
        public byte[]? PaymentImage { get; set; }
    }

}
