using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace HotelBookingSystem.Pages.Staff
{
    public class EventBookingItem
    {
        public int      EventBookingId { get; set; }
        public string   GuestName      { get; set; } = "";
        public string   GuestEmail     { get; set; } = "";
        public string   EventTitle     { get; set; } = "";
        public DateTime EventDate      { get; set; }
        public string   Location       { get; set; } = "";
        public decimal  Price          { get; set; }
        public string   Status         { get; set; } = "";
        public DateTime BookedAt       { get; set; }
    }

    public class EventBookingsModel : PageModel
    {
        private readonly string _conn;
        public EventBookingsModel(string connectionString) => _conn = connectionString;

        public List<EventBookingItem> Bookings { get; set; } = new();

        public void OnGet()
        {
            var role = HttpContext.Session.GetString("UserRole");
            if (role != "STAFF" && role != "ADMIN")
            {
                Response.Redirect("/Login?returnUrl=/Staff/EventBookings");
                return;
            }
            LoadBookings();
        }

        private void LoadBookings()
        {
            using var conn = new SqlConnection(_conn);
            conn.Open();

            string sql = @"
                SELECT eb.eventBookingId, u.fullName, u.email,
                       e.title, e.eventDate, e.location, e.price,
                       eb.status, eb.bookedAt
                FROM EventBookings eb
                JOIN Users  u ON eb.userId  = u.userId
                JOIN Events e ON eb.eventId = e.eventId
                ORDER BY eb.bookedAt DESC";

            using var cmd    = new SqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                Bookings.Add(new EventBookingItem
                {
                    EventBookingId = (int)reader["eventBookingId"],
                    GuestName      = reader["fullName"].ToString()!,
                    GuestEmail     = reader["email"].ToString()!,
                    EventTitle     = reader["title"].ToString()!,
                    EventDate      = (DateTime)reader["eventDate"],
                    Location       = reader["location"].ToString()!,
                    Price          = (decimal)reader["price"],
                    Status         = reader["status"].ToString()!,
                    BookedAt       = (DateTime)reader["bookedAt"]
                });
            }
        }
    }
}
