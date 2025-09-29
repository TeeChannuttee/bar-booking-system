using Microsoft.AspNetCore.SignalR;

namespace BarBookingSystem.Hubs
{
    public class BookingHub : Hub
    {
        public async Task SendMessage(string user, string message)
        {
            await Clients.All.SendAsync("ReceiveMessage", user, message);
        }

        public async Task UpdateBookingStatus(string bookingCode, string status)
        {
            await Clients.All.SendAsync("BookingStatusUpdated", bookingCode, status);
        }

        public async Task NotifyTableAvailable(int tableId)
        {
            await Clients.All.SendAsync("TableAvailable", tableId);
        }
    }
}
