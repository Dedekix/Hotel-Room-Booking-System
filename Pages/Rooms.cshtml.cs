using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace HotelBookingSystem.Pages
{
    public class RoomDisplay
    {
        public int     RoomId        { get; set; }
        public string  RoomNumber    { get; set; } = "";
        public string  Type          { get; set; } = "";
        public decimal PricePerNight { get; set; }
        public int     Capacity      { get; set; }
        public bool    IsAvailable   { get; set; }
        public string  Description   { get; set; } = "";
        public string  DisplayStatus { get; set; } = "";
        public string  ImagePath     { get; set; } = "";
    }

    public class RoomsModel : PageModel
    {
        private readonly string _conn;
        public RoomsModel(string connectionString) => _conn = connectionString;

        public List<RoomDisplay> Rooms { get; set; } = new();

        public void OnGet()
        {
            using var conn = new SqlConnection(_conn);
            conn.Open();

            string sql = @"
                SELECT r.roomId, r.roomNumber, r.type, r.pricePerNight, r.capacity,
                       r.isAvailable, r.description,
                       (SELECT COUNT(*) FROM Bookings b
                        WHERE b.roomId = r.roomId AND b.status = 'CHECKED_IN') AS occupiedCount
                FROM Rooms r
                ORDER BY r.roomNumber";

            using var cmd    = new SqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                bool isAvailable   = (bool)reader["isAvailable"];
                int  occupiedCount = (int)reader["occupiedCount"];
                string type        = reader["type"].ToString()!;

                string status = isAvailable    ? "Available"
                              : occupiedCount > 0 ? "Occupied"
                              : "Maintenance";

                // Map a default image per type
                string img = type.ToUpper() switch
                {
                    "SUITE"  => "Images/room301.jpg",
                    "DELUXE" => "Images/room201.jpg",
                    _        => "Images/room101.jpg"
                };

                Rooms.Add(new RoomDisplay
                {
                    RoomId        = (int)reader["roomId"],
                    RoomNumber    = reader["roomNumber"].ToString()!,
                    Type          = type,
                    PricePerNight = (decimal)reader["pricePerNight"],
                    Capacity      = (int)reader["capacity"],
                    IsAvailable   = isAvailable,
                    Description   = reader["description"]?.ToString() ?? "",
                    DisplayStatus = status,
                    ImagePath     = img
                });
            }
        }
    }
}
