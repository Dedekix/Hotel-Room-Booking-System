using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace HotelBookingSystem.Pages
{
    public class RoomBookingItem
    {
        public int      BookingId   { get; set; }
        public string   RoomNumber  { get; set; } = "";
        public string   RoomType    { get; set; } = "";
        public DateTime CheckIn     { get; set; }
        public DateTime CheckOut    { get; set; }
        public int      GuestCount  { get; set; }
        public decimal  TotalPrice  { get; set; }
        public string   Status      { get; set; } = "";
    }

    public class EventReservationItem
    {
        public int      EventBookingId { get; set; }
        public string   EventTitle     { get; set; } = "";
        public DateTime EventDate      { get; set; }
        public string   Location       { get; set; } = "";
        public decimal  Price          { get; set; }
        public string   Status         { get; set; } = "";
    }

    public class MyBookingsModel : PageModel
    {
        private readonly string _conn;
        public MyBookingsModel(string connectionString) => _conn = connectionString;

        public List<RoomBookingItem>      RoomBookings      { get; set; } = new();
        public List<EventReservationItem> EventReservations { get; set; } = new();

        public IActionResult OnGet()
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
                return Redirect("/Login?returnUrl=/MyBookings");

            using var conn = new SqlConnection(_conn);
            conn.Open();

            using (var cmd = new SqlCommand(@"
                SELECT b.bookingId, r.roomNumber, r.type,
                       b.checkInDate, b.checkOutDate, b.guestCount, b.totalPrice, b.status
                FROM Bookings b
                JOIN Rooms r ON b.roomId = r.roomId
                WHERE b.userId = @uid
                ORDER BY b.bookingId DESC", conn))
            {
                cmd.Parameters.AddWithValue("@uid", int.Parse(userId));
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    RoomBookings.Add(new RoomBookingItem
                    {
                        BookingId  = (int)reader["bookingId"],
                        RoomNumber = reader["roomNumber"].ToString()!,
                        RoomType   = reader["type"].ToString()!,
                        CheckIn    = (DateTime)reader["checkInDate"],
                        CheckOut   = (DateTime)reader["checkOutDate"],
                        GuestCount = (int)reader["guestCount"],
                        TotalPrice = (decimal)reader["totalPrice"],
                        Status     = reader["status"].ToString()!
                    });
            }

            using (var cmd = new SqlCommand(@"
                SELECT eb.eventBookingId, e.title, e.eventDate, e.location, e.price, eb.status
                FROM EventBookings eb
                JOIN Events e ON eb.eventId = e.eventId
                WHERE eb.userId = @uid
                ORDER BY eb.eventBookingId DESC", conn))
            {
                cmd.Parameters.AddWithValue("@uid", int.Parse(userId));
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    EventReservations.Add(new EventReservationItem
                    {
                        EventBookingId = (int)reader["eventBookingId"],
                        EventTitle     = reader["title"].ToString()!,
                        EventDate      = (DateTime)reader["eventDate"],
                        Location       = reader["location"].ToString()!,
                        Price          = (decimal)reader["price"],
                        Status         = reader["status"].ToString()!
                    });
            }

            return Page();
        }
    }
}
