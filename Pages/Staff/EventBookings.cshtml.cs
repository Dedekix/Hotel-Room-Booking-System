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
        private const int PageSize = 5;
        public EventBookingsModel(string connectionString) => _conn = connectionString;

        public List<EventBookingItem> Bookings     { get; set; } = new();
        public int     CurrentPage   { get; set; } = 1;
        public int     TotalPages    { get; set; } = 1;
        public int     TotalCount    { get; set; }
        public string? SuccessMessage { get; set; }

        public void OnGet(int p = 1)
        {
            var role = HttpContext.Session.GetString("UserRole");
            if (role != "STAFF" && role != "ADMIN") { Response.Redirect("/Login?returnUrl=/Staff/EventBookings"); return; }
            CurrentPage = Math.Max(1, p);
            LoadBookings();
        }

        public IActionResult OnPostConfirm(int eventBookingId, decimal amount)
        {
            var role = HttpContext.Session.GetString("UserRole");
            if (role != "STAFF" && role != "ADMIN") return Redirect("/Login");

            using var conn = new SqlConnection(_conn);
            conn.Open();

            using (var cmd = new SqlCommand(@"
                INSERT INTO Payments (eventBookingId, amount, method, status)
                VALUES (@eid, @amt, 'In Person', 'COMPLETED')", conn))
            {
                cmd.Parameters.AddWithValue("@eid", eventBookingId);
                cmd.Parameters.AddWithValue("@amt", amount);
                cmd.ExecuteNonQuery();
            }

            using (var cmd = new SqlCommand(
                "UPDATE EventBookings SET status = 'CONFIRMED' WHERE eventBookingId = @id", conn))
            {
                cmd.Parameters.AddWithValue("@id", eventBookingId);
                cmd.ExecuteNonQuery();
            }

            SuccessMessage = "Payment confirmed successfully.";
            LoadBookings();
            return Page();
        }

        private void LoadBookings()
        {
            using var conn = new SqlConnection(_conn);
            conn.Open();

            using (var cmd = new SqlCommand("SELECT COUNT(*) FROM EventBookings", conn))
                TotalCount = (int)cmd.ExecuteScalar();

            TotalPages  = Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
            CurrentPage = Math.Min(CurrentPage, TotalPages);

            string sql = @"
                SELECT eb.eventBookingId, u.fullName, u.email,
                       e.title, e.eventDate, e.location, e.price,
                       eb.status, eb.bookedAt
                FROM EventBookings eb
                JOIN Users  u ON eb.userId  = u.userId
                JOIN Events e ON eb.eventId = e.eventId
                ORDER BY eb.bookedAt DESC
                OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY";

            using var cmd2   = new SqlCommand(sql, conn);
            cmd2.Parameters.AddWithValue("@skip", (CurrentPage - 1) * PageSize);
            cmd2.Parameters.AddWithValue("@take", PageSize);
            using var reader = cmd2.ExecuteReader();
            while (reader.Read())
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
