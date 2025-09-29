using BarBookingSystem.Models;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
namespace BarBookingSystem.Services
{
    public interface ICalendarService
    {
        Task<string> CreateEventAsync(Booking booking);
        Task UpdateEventAsync(string eventId, Booking booking);
        Task DeleteEventAsync(string eventId);
    }

    public class GoogleCalendarService : ICalendarService
    {
        private readonly CalendarService _calendarService;
        private readonly string _calendarId;

        public GoogleCalendarService(IConfiguration config)
        {
            // Initialize Google Calendar API with Service Account (FREE)
            var credential = GoogleCredential.FromFile("path/to/service-account-key.json")
                .CreateScoped(CalendarService.Scope.Calendar);

            _calendarService = new CalendarService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "Bar Booking System"
            });

            _calendarId = config["GoogleCalendar:CalendarId"]; // or "primary"
        }

        public async Task<string> CreateEventAsync(Booking booking)
        {
            var eventItem = new Event()
            {
                Summary = $"Booking: {booking.User.FullName} - Table {booking.Table.TableNumber}",
                Location = booking.Table.Branch.Address,
                Description = $@"
                    Booking Code: {booking.BookingCode}
                    Customer: {booking.User.FullName}
                    Phone: {booking.User.PhoneNumber}
                    Guests: {booking.NumberOfGuests}
                    Table: {booking.Table.TableNumber} ({booking.Table.Zone})
                    Special Requests: {booking.SpecialRequests ?? "None"}
                ",
                Start = new EventDateTime()
                {
                    DateTime = booking.BookingDate.Add(booking.StartTime),
                    TimeZone = "Asia/Bangkok",
                },
                End = new EventDateTime()
                {
                    DateTime = booking.BookingDate.Add(booking.EndTime),
                    TimeZone = "Asia/Bangkok",
                },
                Reminders = new Event.RemindersData()
                {
                    UseDefault = false,
                    Overrides = new EventReminder[]
                    {
                        new EventReminder() { Method = "email", Minutes = 24 * 60 }, // 1 day before
                        new EventReminder() { Method = "popup", Minutes = 120 }, // 2 hours before
                    }
                }
            };

            var request = _calendarService.Events.Insert(eventItem, _calendarId);
            var createdEvent = await request.ExecuteAsync();
            return createdEvent.Id;
        }

        public async Task UpdateEventAsync(string eventId, Booking booking)
        {
            var eventItem = await _calendarService.Events.Get(_calendarId, eventId).ExecuteAsync();

            eventItem.Start = new EventDateTime()
            {
                DateTime = booking.BookingDate.Add(booking.StartTime),
                TimeZone = "Asia/Bangkok",
            };
            eventItem.End = new EventDateTime()
            {
                DateTime = booking.BookingDate.Add(booking.EndTime),
                TimeZone = "Asia/Bangkok",
            };

            await _calendarService.Events.Update(eventItem, _calendarId, eventId).ExecuteAsync();
        }

        public async Task DeleteEventAsync(string eventId)
        {
            await _calendarService.Events.Delete(_calendarId, eventId).ExecuteAsync();
        }
    }
}

