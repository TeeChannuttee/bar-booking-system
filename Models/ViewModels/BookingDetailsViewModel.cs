namespace BarBookingSystem.Models.ViewModels
{
    public class BookingDetailsViewModel
    {
        public Booking Booking { get; set; }
        public bool CanCancel { get; set; }
        public bool CanModify { get; set; }
        public string QRCodeBase64 { get; set; }
        public decimal RefundAmount { get; set; }
    }
}
