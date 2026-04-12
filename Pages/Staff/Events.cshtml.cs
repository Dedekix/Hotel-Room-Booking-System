using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace HotelBookingSystem.Pages.Staff
{
    public class EventItem
    {
        public int EventId { get; set; }
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public DateTime EventDate { get; set; }
        public string Location { get; set; } = "";
        public int Capacity { get; set; }
        public decimal Price { get; set; }
        public int BookingCount { get; set; }
    }

    public class EventsModel : PageModel
    {
        private readonly string _conn;
        public EventsModel(string connectionString) => _conn = connectionString;

        public List<EventItem> Events { get; set; } = new();
        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }

        public void OnGet()
        {
            var role = HttpContext.Session.GetString("UserRole");
            if (role != "STAFF" && role != "ADMIN") { Response.Redirect("/Login"); return; }
            LoadEvents();
        }

        public IActionResult OnPostAdd(string title, string description, DateTime eventDate,
            string location, int capacity, decimal price)
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(location))
            {
                ErrorMessage = "Title and location are required.";
                LoadEvents();
                return Page();
            }

            var staffId = HttpContext.Session.GetString("UserId");
            using var conn = new SqlConnection(_conn);
            conn.Open();

            string insert = @"INSERT INTO Events (title, description, eventDate, location, capacity, price, createdBy)
                              VALUES (@title, @desc, @date, @loc, @cap, @price, @by)";
            using var cmd = new SqlCommand(insert, conn);
            cmd.Parameters.AddWithValue("@title", title);
            cmd.Parameters.AddWithValue("@desc",  description ?? "");
            cmd.Parameters.AddWithValue("@date",  eventDate);
            cmd.Parameters.AddWithValue("@loc",   location);
            cmd.Parameters.AddWithValue("@cap",   capacity);
            cmd.Parameters.AddWithValue("@price", price);
            cmd.Parameters.AddWithValue("@by",    int.Parse(staffId!));
            cmd.ExecuteNonQuery();

            SuccessMessage = $"Event \"{title}\" added successfully.";
            LoadEvents();
            return Page();
        }

        public IActionResult OnPostEdit(int eventId, string title, string description,
            DateTime eventDate, string location, int capacity, decimal price)
        {
            using var conn = new SqlConnection(_conn);
            conn.Open();

            string update = @"UPDATE Events SET title=@title, description=@desc, eventDate=@date,
                              location=@loc, capacity=@cap, price=@price WHERE eventId=@id";
            using var cmd = new SqlCommand(update, conn);
            cmd.Parameters.AddWithValue("@title", title);
            cmd.Parameters.AddWithValue("@desc",  description ?? "");
            cmd.Parameters.AddWithValue("@date",  eventDate);
            cmd.Parameters.AddWithValue("@loc",   location);
            cmd.Parameters.AddWithValue("@cap",   capacity);
            cmd.Parameters.AddWithValue("@price", price);
            cmd.Parameters.AddWithValue("@id",    eventId);
            cmd.ExecuteNonQuery();

            SuccessMessage = $"Event \"{title}\" updated successfully.";
            LoadEvents();
            return Page();
        }

        public IActionResult OnPostDelete(int eventId)
        {
            using var conn = new SqlConnection(_conn);
            conn.Open();

            using (var cmd = new SqlCommand("DELETE FROM EventBookings WHERE eventId=@id", conn))
            {
                cmd.Parameters.AddWithValue("@id", eventId);
                cmd.ExecuteNonQuery();
            }
            using (var cmd = new SqlCommand("DELETE FROM Events WHERE eventId=@id", conn))
            {
                cmd.Parameters.AddWithValue("@id", eventId);
                cmd.ExecuteNonQuery();
            }

            SuccessMessage = "Event deleted successfully.";
            LoadEvents();
            return Page();
        }

        private void LoadEvents()
        {
            Events.Clear();
            using var conn = new SqlConnection(_conn);
            conn.Open();

            string sql = @"
                SELECT e.eventId, e.title, e.description, e.eventDate, e.location, e.capacity, e.price,
                       (SELECT COUNT(*) FROM EventBookings eb
                        WHERE eb.eventId = e.eventId AND eb.status != 'CANCELLED') AS bookingCount
                FROM Events e
                ORDER BY e.eventDate";

            using var cmd = new SqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                Events.Add(new EventItem
                {
                    EventId      = (int)reader["eventId"],
                    Title        = reader["title"].ToString()!,
                    Description  = reader["description"]?.ToString() ?? "",
                    EventDate    = (DateTime)reader["eventDate"],
                    Location     = reader["location"].ToString()!,
                    Capacity     = (int)reader["capacity"],
                    Price        = (decimal)reader["price"],
                    BookingCount = (int)reader["bookingCount"]
                });
            }
        }
    }
}
