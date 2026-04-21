using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace HotelBookingSystem.Pages.Customer
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
        private const int PageSize = 5;
        public MyBookingsModel(string connectionString) => _conn = connectionString;

        public List<RoomBookingItem>      RoomBookings      { get; set; } = new();
        public List<EventReservationItem> EventReservations { get; set; } = new();

        public int RoomPage       { get; set; } = 1;
        public int RoomTotalPages { get; set; } = 1;
        public int EventPage      { get; set; } = 1;
        public int EventTotalPages{ get; set; } = 1;

        public IActionResult OnGet(int rp = 1, int ep = 1)
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
                return Redirect("/Login?returnUrl=/Customer/MyBookings");

            int uid = int.Parse(userId);
            RoomPage  = Math.Max(1, rp);
            EventPage = Math.Max(1, ep);

            using var conn = new SqlConnection(_conn);
            conn.Open();

            // Room bookings
            int roomCount;
            using (var cmd = new SqlCommand("SELECT COUNT(*) FROM Bookings WHERE userId = @uid", conn))
            {
                cmd.Parameters.AddWithValue("@uid", uid);
                roomCount = (int)cmd.ExecuteScalar();
            }
            RoomTotalPages = Math.Max(1, (int)Math.Ceiling(roomCount / (double)PageSize));
            RoomPage       = Math.Min(RoomPage, RoomTotalPages);

            using (var cmd = new SqlCommand(@"
                SELECT b.bookingId, r.roomNumber, r.type,
                       b.checkInDate, b.checkOutDate, b.guestCount, b.totalPrice, b.status
                FROM Bookings b
                JOIN Rooms r ON b.roomId = r.roomId
                WHERE b.userId = @uid
                ORDER BY b.bookingId DESC
                OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY", conn))
            {
                cmd.Parameters.AddWithValue("@uid",  uid);
                cmd.Parameters.AddWithValue("@skip", (RoomPage - 1) * PageSize);
                cmd.Parameters.AddWithValue("@take", PageSize);
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

            // Event reservations
            int eventCount;
            using (var cmd = new SqlCommand("SELECT COUNT(*) FROM EventBookings WHERE userId = @uid", conn))
            {
                cmd.Parameters.AddWithValue("@uid", uid);
                eventCount = (int)cmd.ExecuteScalar();
            }
            EventTotalPages = Math.Max(1, (int)Math.Ceiling(eventCount / (double)PageSize));
            EventPage       = Math.Min(EventPage, EventTotalPages);

            using (var cmd = new SqlCommand(@"
                SELECT eb.eventBookingId, e.title, e.eventDate, e.location, e.price, eb.status
                FROM EventBookings eb
                JOIN Events e ON eb.eventId = e.eventId
                WHERE eb.userId = @uid
                ORDER BY eb.eventBookingId DESC
                OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY", conn))
            {
                cmd.Parameters.AddWithValue("@uid",  uid);
                cmd.Parameters.AddWithValue("@skip", (EventPage - 1) * PageSize);
                cmd.Parameters.AddWithValue("@take", PageSize);
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

        public IActionResult OnPostCancelRoom(int bookingId, int rp = 1, int ep = 1)
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId)) return Redirect("/Login");

            using var conn = new SqlConnection(_conn);
            conn.Open();
            using var cmd = new SqlCommand(
                "UPDATE Bookings SET status = 'CANCELLED' WHERE bookingId = @id AND userId = @uid AND status = 'PENDING'", conn);
            cmd.Parameters.AddWithValue("@id",  bookingId);
            cmd.Parameters.AddWithValue("@uid", int.Parse(userId));
            cmd.ExecuteNonQuery();

            return RedirectToPage(new { rp, ep });
        }

        public IActionResult OnPostCancelEvent(int eventBookingId, int rp = 1, int ep = 1)
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId)) return Redirect("/Login");

            using var conn = new SqlConnection(_conn);
            conn.Open();
            using var cmd = new SqlCommand(
                "UPDATE EventBookings SET status = 'CANCELLED' WHERE eventBookingId = @id AND userId = @uid", conn);
            cmd.Parameters.AddWithValue("@id",  eventBookingId);
            cmd.Parameters.AddWithValue("@uid", int.Parse(userId));
            cmd.ExecuteNonQuery();

            return RedirectToPage(new { rp, ep });
        }
    }
}
