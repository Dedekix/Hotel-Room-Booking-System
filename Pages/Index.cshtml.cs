using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace HotelBookingSystem.Pages
{
    public class HomeEventItem
    {
        public int     EventId   { get; set; }
        public string  Title     { get; set; } = "";
        public string  EventDate { get; set; } = "";
        public string  Location  { get; set; } = "";
        public int     Capacity  { get; set; }
        public decimal Price     { get; set; }
        public string  ImagePath { get; set; } = "";
        public string  Badge     { get; set; } = "";
        public string  Description { get; set; } = "";
    }

    public class IndexModel : PageModel
    {
        private readonly string _conn;
        public IndexModel(string connectionString) => _conn = connectionString;

        public bool IsLoggedIn { get; set; }
        public List<HomeEventItem> Events { get; set; } = new();

        public void OnGet()
        {
            IsLoggedIn = HttpContext.Session.GetString("UserId") != null;

            using var conn = new SqlConnection(_conn);
            conn.Open();
            using var cmd    = new SqlCommand("SELECT TOP 4 eventId, title, description, eventDate, location, capacity, price, imagePath FROM Events ORDER BY eventDate", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var date  = (DateTime)reader["eventDate"];
                var title = reader["title"].ToString()!;
                var image = !string.IsNullOrEmpty(reader["imagePath"]?.ToString())
                    ? reader["imagePath"].ToString()!
                    : title.ToLower() switch
                    {
                        var t when t.Contains("gala")   => "Images/event-gala.jpg",
                        var t when t.Contains("spa")    => "Images/event-spa.jpg",
                        var t when t.Contains("brunch") => "Images/event-gala.jpg",
                        _                               => "Images/event-spa.jpg"
                    };
                Events.Add(new HomeEventItem
                {
                    EventId     = (int)reader["eventId"],
                    Title       = title,
                    Description = reader["description"]?.ToString() ?? "",
                    EventDate   = date.ToString("MMM d, yyyy"),
                    Location    = reader["location"].ToString()!,
                    Capacity    = (int)reader["capacity"],
                    Price       = (decimal)reader["price"],
                    ImagePath   = image,
                    Badge       = date.ToString("MMMM")
                });
            }
        }
    }
}
