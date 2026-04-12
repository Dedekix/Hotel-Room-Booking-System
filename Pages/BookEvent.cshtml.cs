using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace HotelBookingSystem.Pages
{
    public class BookEventModel : PageModel
    {
        private readonly string _conn;
        public BookEventModel(string connectionString) => _conn = connectionString;

        public int     EventId          { get; set; }
        public string  EventTitle       { get; set; } = "";
        public string  Description      { get; set; } = "";
        public string  EventDateDisplay { get; set; } = "";
        public string  Location         { get; set; } = "";
        public int     Capacity         { get; set; }
        public int     SpotsLeft        { get; set; }
        public decimal Price            { get; set; }
        public string  ImagePath        { get; set; } = "";
        public string  PrefillName      { get; set; } = "";
        public string  PrefillEmail     { get; set; } = "";
        public bool    Confirmed        { get; set; }
        public string? ErrorMessage     { get; set; }

        public IActionResult OnGet(int eventId)
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (userId == null) return RedirectToPage("/Login");

            if (!LoadEvent(eventId)) return RedirectToPage("/Events");

            // Pre-fill from session user
            using var conn = new SqlConnection(_conn);
            conn.Open();
            using var cmd = new SqlCommand("SELECT fullName, email FROM Users WHERE userId=@id", conn);
            cmd.Parameters.AddWithValue("@id", int.Parse(userId));
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                PrefillName  = reader["fullName"].ToString()!;
                PrefillEmail = reader["email"].ToString()!;
            }
            return Page();
        }

        public IActionResult OnPost(int eventId, string fullName, string email)
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (userId == null) return RedirectToPage("/Login");

            if (!LoadEvent(eventId))
            {
                ErrorMessage = "Event not found.";
                return Page();
            }

            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email))
            {
                ErrorMessage = "Full name and email are required.";
                return Page();
            }

            using var conn = new SqlConnection(_conn);
            conn.Open();

            // Verify email matches the logged-in user
            int uid = int.Parse(userId);
            using (var cmd = new SqlCommand(
                "SELECT COUNT(*) FROM Users WHERE userId=@id AND email=@email AND isActive=1", conn))
            {
                cmd.Parameters.AddWithValue("@id",    uid);
                cmd.Parameters.AddWithValue("@email", email);
                if ((int)cmd.ExecuteScalar() == 0)
                {
                    ErrorMessage = "Email does not match your account.";
                    PrefillName  = fullName;
                    PrefillEmail = email;
                    return Page();
                }
            }

            // Check for duplicate booking
            using (var cmd = new SqlCommand(
                "SELECT COUNT(*) FROM EventBookings WHERE eventId=@eid AND userId=@uid AND status!='CANCELLED'", conn))
            {
                cmd.Parameters.AddWithValue("@eid", eventId);
                cmd.Parameters.AddWithValue("@uid", uid);
                if ((int)cmd.ExecuteScalar() > 0)
                {
                    ErrorMessage = "You have already reserved a spot for this event.";
                    return Page();
                }
            }

            // Check capacity
            if (SpotsLeft <= 0)
            {
                ErrorMessage = "Sorry, this event is fully booked.";
                return Page();
            }

            // Insert booking
            using (var cmd = new SqlCommand(
                "INSERT INTO EventBookings (eventId, userId, status) VALUES (@eid, @uid, 'CONFIRMED')", conn))
            {
                cmd.Parameters.AddWithValue("@eid", eventId);
                cmd.Parameters.AddWithValue("@uid", uid);
                cmd.ExecuteNonQuery();
            }

            Confirmed = true;
            return Page();
        }

        private bool LoadEvent(int eventId)
        {
            using var conn = new SqlConnection(_conn);
            conn.Open();

            string sql = @"
                SELECT e.eventId, e.title, e.description, e.eventDate, e.location, e.capacity, e.price,
                       (SELECT COUNT(*) FROM EventBookings eb
                        WHERE eb.eventId = e.eventId AND eb.status != 'CANCELLED') AS booked
                FROM Events e WHERE e.eventId = @id";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", eventId);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return false;

            var title = reader["title"].ToString()!;
            EventId          = (int)reader["eventId"];
            EventTitle       = title;
            Description      = reader["description"]?.ToString() ?? "";
            EventDateDisplay = ((DateTime)reader["eventDate"]).ToString("MMMM d, yyyy — h:mm tt");
            Location         = reader["location"].ToString()!;
            Capacity         = (int)reader["capacity"];
            Price            = (decimal)reader["price"];
            SpotsLeft        = Capacity - (int)reader["booked"];
            ImagePath        = title.ToLower() switch
            {
                var t when t.Contains("talent")  => "Images/talent.jpg",
                var t when t.Contains("culture") => "Images/culture.jpg",
                var t when t.Contains("spa")     => "Images/event-spa.jpg",
                _                                => "Images/event-gala.jpg"
            };
            return true;
        }
    }
}
