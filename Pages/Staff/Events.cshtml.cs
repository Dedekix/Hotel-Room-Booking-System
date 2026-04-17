using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace HotelBookingSystem.Pages.Staff
{
    public class EventItem
    {
        public int     EventId       { get; set; }
        public string  Title         { get; set; } = "";
        public string  Description   { get; set; } = "";
        public DateTime EventDate    { get; set; }
        public string  Location      { get; set; } = "";
        public int     Capacity      { get; set; }
        public decimal Price         { get; set; }
        public int     BookingCount   { get; set; }
        public int     CancelledCount  { get; set; }
        public string  ImagePath       { get; set; } = "";
        public int     AvailableSpots => Capacity - BookingCount;
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
            if (role != "ADMIN") { Response.Redirect("/Login?returnUrl=/Staff/Events"); return; }
            if (TempData["SuccessMessage"] is string msg) SuccessMessage = msg;
            LoadEvents();
        }

        public IActionResult OnPostAdd(string title, string description, DateTime eventDate,
            string location, int capacity, decimal price, IFormFile? imageFile)
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(location))
            {
                ErrorMessage = "Title and location are required.";
                LoadEvents();
                return Page();
            }

            // Handle image upload
            string imagePath = "";
            if (imageFile != null && imageFile.Length > 0)
            {
                var fileName = Path.GetFileName(imageFile.FileName);
                var savePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Images", fileName);
                using var stream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.Read);
                imageFile.CopyTo(stream);
                imagePath = $"Images/{fileName}";
            }

            var staffId = HttpContext.Session.GetString("UserId");
            using var conn = new SqlConnection(_conn);
            conn.Open();

            string insert = @"INSERT INTO Events (title, description, eventDate, location, capacity, price, createdBy, imagePath)
                              VALUES (@title, @desc, @date, @loc, @cap, @price, @by, @img)";
            using var cmd = new SqlCommand(insert, conn);
            cmd.Parameters.AddWithValue("@title", title);
            cmd.Parameters.AddWithValue("@desc",  description ?? "");
            cmd.Parameters.AddWithValue("@date",  eventDate);
            cmd.Parameters.AddWithValue("@loc",   location);
            cmd.Parameters.AddWithValue("@cap",   capacity);
            cmd.Parameters.AddWithValue("@price", price);
            cmd.Parameters.AddWithValue("@by",    int.Parse(staffId!));
            cmd.Parameters.AddWithValue("@img",   imagePath);
            cmd.ExecuteNonQuery();

            SuccessMessage = $"Event \"{title}\" added successfully.";
            LoadEvents();
            return Page();
        }

        public IActionResult OnPostEdit(int eventId, string title, string description,
            DateTime eventDate, string location, int capacity, decimal price, IFormFile? imageFile)
        {
            using var conn = new SqlConnection(_conn);
            conn.Open();

            string imagePath = "";
            if (imageFile != null && imageFile.Length > 0)
            {
                var fileName = Path.GetFileName(imageFile.FileName);
                var savePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Images", fileName);
                using var stream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.Read);
                imageFile.CopyTo(stream);
                imagePath = $"Images/{fileName}";
            }

            string update = imagePath != ""
                ? @"UPDATE Events SET title=@title, description=@desc, eventDate=@date,
                    location=@loc, capacity=@cap, price=@price, imagePath=@img WHERE eventId=@id"
                : @"UPDATE Events SET title=@title, description=@desc, eventDate=@date,
                    location=@loc, capacity=@cap, price=@price WHERE eventId=@id";

            using var cmd = new SqlCommand(update, conn);
            cmd.Parameters.AddWithValue("@title", title);
            cmd.Parameters.AddWithValue("@desc",  description ?? "");
            cmd.Parameters.AddWithValue("@date",  eventDate);
            cmd.Parameters.AddWithValue("@loc",   location);
            cmd.Parameters.AddWithValue("@cap",   capacity);
            cmd.Parameters.AddWithValue("@price", price);
            cmd.Parameters.AddWithValue("@id",    eventId);
            if (imagePath != "") cmd.Parameters.AddWithValue("@img", imagePath);
            cmd.ExecuteNonQuery();

            TempData["SuccessMessage"] = $"Event \"{title}\" updated successfully.";
            return RedirectToPage();
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
                SELECT e.eventId, e.title, e.description, e.eventDate, e.location, e.capacity, e.price, e.imagePath,
                       (SELECT COUNT(*) FROM EventBookings eb
                        WHERE eb.eventId = e.eventId AND eb.status != 'CANCELLED') AS bookingCount,
                       (SELECT COUNT(*) FROM EventBookings eb
                        WHERE eb.eventId = e.eventId AND eb.status = 'CANCELLED') AS cancelledCount
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
                    BookingCount   = (int)reader["bookingCount"],
                    CancelledCount = (int)reader["cancelledCount"],
                    ImagePath      = reader["imagePath"]?.ToString() ?? ""
                });
            }
        }
    }
}
