using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace HotelBookingSystem.Pages
{
    public class EventDisplay
    {
        public int     EventId     { get; set; }
        public string  Title       { get; set; } = "";
        public string  Description { get; set; } = "";
        public string  EventDate   { get; set; } = "";
        public string  Location    { get; set; } = "";
        public int     Capacity    { get; set; }
        public decimal Price       { get; set; }
        public string  ImagePath   { get; set; } = "";
        public string  Badge       { get; set; } = "";
    }

    public class EventsModel : PageModel
    {
        private readonly string _conn;
        public EventsModel(string connectionString) => _conn = connectionString;

        public List<EventDisplay> Events { get; set; } = new();
        public bool   Reserved     { get; set; }
        public string ErrorMessage { get; set; } = "";

        public void OnGet()
        {
            LoadEvents();
        }

        public IActionResult OnPost(int eventId, int guests)
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (userId == null) return RedirectToPage("/Login");

            using var conn = new SqlConnection(_conn);
            conn.Open();

            // Check capacity
            string capacityCheck = @"
                SELECT e.capacity,
                       (SELECT COUNT(*) FROM EventBookings eb WHERE eb.eventId = e.eventId AND eb.status != 'CANCELLED') AS booked
                FROM Events e WHERE e.eventId = @eventId";

            bool full = false;
            using (var cmd = new SqlCommand(capacityCheck, conn))
            {
                cmd.Parameters.AddWithValue("@eventId", eventId);
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    int capacity = (int)reader["capacity"];
                    int booked   = (int)reader["booked"];
                    if (booked + guests > capacity) full = true;
                }
            } // reader and cmd disposed here before INSERT

            if (full)
            {
                ErrorMessage = "Not enough spots available for this event.";
                LoadEvents();
                return Page();
            }

            // Save reservation
            string insert = @"
                INSERT INTO EventBookings (eventId, userId, status)
                VALUES (@eventId, @userId, 'CONFIRMED')";

            using (var cmd = new SqlCommand(insert, conn))
            {
                cmd.Parameters.AddWithValue("@eventId", eventId);
                cmd.Parameters.AddWithValue("@userId",  int.Parse(userId));
                cmd.ExecuteNonQuery();
            }

            Reserved = true;
            LoadEvents();
            return Page();
        }

        private void LoadEvents()
        {
            using var conn = new SqlConnection(_conn);
            conn.Open();
            string sql = "SELECT eventId, title, description, eventDate, location, capacity, price FROM Events ORDER BY eventDate";
            using var cmd    = new SqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var date  = (DateTime)reader["eventDate"];
                var title = reader["title"].ToString()!;

                string img = title.ToLower() switch
                {
                    var t when t.Contains("talent") => "Images/talent.jpg",
                    var t when t.Contains("culture") => "Images/culture.jpg",
                    var t when t.Contains("spa")    => "Images/event-spa.jpg",
                    _                               => "Images/event-gala.jpg"
                };

                Events.Add(new EventDisplay
                {
                    EventId     = (int)reader["eventId"],
                    Title       = title,
                    Description = reader["description"]?.ToString() ?? "",
                    EventDate   = date.ToString("MMMM d, yyyy — h:mm tt"),
                    Location    = reader["location"].ToString()!,
                    Capacity    = (int)reader["capacity"],
                    Price       = (decimal)reader["price"],
                    ImagePath   = img,
                    Badge       = date.ToString("MMMM")
                });
            }
        }
    }
}
