using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace HotelBookingSystem.Pages.Staff
{
    public class RoomItem
    {
        public int RoomId { get; set; }
        public string RoomNumber { get; set; } = "";
        public string Type { get; set; } = "";
        public decimal PricePerNight { get; set; }
        public int Capacity { get; set; }
        public bool IsAvailable { get; set; }
        public string Description { get; set; } = "";
        public string DisplayStatus { get; set; } = ""; // Available | Occupied | Maintenance
    }

    public class RoomsModel : PageModel
    {
        private readonly string _conn;
        public RoomsModel(string connectionString) => _conn = connectionString;

        public List<RoomItem> Rooms { get; set; } = new();
        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }

        public void OnGet()
        {
            LoadRooms();
        }

        // ── Add Room ──────────────────────────────────────────
        public IActionResult OnPostAdd(string roomNumber, string type,
            decimal pricePerNight, int capacity, string description)
        {
            if (string.IsNullOrWhiteSpace(roomNumber) || string.IsNullOrWhiteSpace(type))
            {
                ErrorMessage = "Room number and type are required.";
                LoadRooms();
                return Page();
            }

            using var conn = new SqlConnection(_conn);
            conn.Open();

            string check = "SELECT COUNT(*) FROM Rooms WHERE roomNumber = @rn";
            using (var cmd = new SqlCommand(check, conn))
            {
                cmd.Parameters.AddWithValue("@rn", roomNumber);
                if ((int)cmd.ExecuteScalar() > 0)
                {
                    ErrorMessage = $"Room {roomNumber} already exists.";
                    LoadRooms();
                    return Page();
                }
            }

            string insert = @"INSERT INTO Rooms (roomNumber, type, pricePerNight, capacity, isAvailable, description)
                              VALUES (@rn, @type, @price, @cap, 1, @desc)";
            using (var cmd = new SqlCommand(insert, conn))
            {
                cmd.Parameters.AddWithValue("@rn",    roomNumber);
                cmd.Parameters.AddWithValue("@type",  type.ToUpper());
                cmd.Parameters.AddWithValue("@price", pricePerNight);
                cmd.Parameters.AddWithValue("@cap",   capacity);
                cmd.Parameters.AddWithValue("@desc",  description ?? "");
                cmd.ExecuteNonQuery();
            }

            SuccessMessage = $"Room {roomNumber} added successfully.";
            LoadRooms();
            return Page();
        }

        // ── Edit Room ─────────────────────────────────────────
        public IActionResult OnPostEdit(int roomId, string roomNumber, string type,
            decimal pricePerNight, int capacity, string description)
        {
            using var conn = new SqlConnection(_conn);
            conn.Open();

            string update = @"UPDATE Rooms SET roomNumber=@rn, type=@type, pricePerNight=@price,
                              capacity=@cap, description=@desc WHERE roomId=@id";
            using var cmd = new SqlCommand(update, conn);
            cmd.Parameters.AddWithValue("@rn",    roomNumber);
            cmd.Parameters.AddWithValue("@type",  type.ToUpper());
            cmd.Parameters.AddWithValue("@price", pricePerNight);
            cmd.Parameters.AddWithValue("@cap",   capacity);
            cmd.Parameters.AddWithValue("@desc",  description ?? "");
            cmd.Parameters.AddWithValue("@id",    roomId);
            cmd.ExecuteNonQuery();

            SuccessMessage = $"Room {roomNumber} updated successfully.";
            LoadRooms();
            return Page();
        }

        // ── Toggle Availability (key button) ──────────────────
        public IActionResult OnPostToggle(int roomId)
        {
            using var conn = new SqlConnection(_conn);
            conn.Open();

            // Block toggle if room is currently occupied
            string checkOccupied = @"SELECT COUNT(*) FROM Bookings
                                     WHERE roomId=@id AND status='CHECKED_IN'";
            using (var cmd = new SqlCommand(checkOccupied, conn))
            {
                cmd.Parameters.AddWithValue("@id", roomId);
                if ((int)cmd.ExecuteScalar() > 0)
                {
                    ErrorMessage = "Cannot change availability — room is currently occupied.";
                    LoadRooms();
                    return Page();
                }
            }

            string toggle = "UPDATE Rooms SET isAvailable = 1 - isAvailable WHERE roomId = @id";
            using (var cmd = new SqlCommand(toggle, conn))
            {
                cmd.Parameters.AddWithValue("@id", roomId);
                cmd.ExecuteNonQuery();
            }

            LoadRooms();
            return Page();
        }

        // ── Load helper ───────────────────────────────────────
        private void LoadRooms()
        {
            Rooms.Clear();
            using var conn = new SqlConnection(_conn);
            conn.Open();

            string sql = @"
                SELECT r.roomId, r.roomNumber, r.type, r.pricePerNight, r.capacity,
                       r.isAvailable, r.description,
                       (SELECT COUNT(*) FROM Bookings b
                        WHERE b.roomId = r.roomId AND b.status = 'CHECKED_IN') AS occupiedCount
                FROM Rooms r
                ORDER BY r.roomNumber";

            using var cmd = new SqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                bool isAvailable  = (bool)reader["isAvailable"];
                int  occupiedCount = (int)reader["occupiedCount"];

                string status = isAvailable ? "Available"
                              : occupiedCount > 0 ? "Occupied"
                              : "Maintenance";

                Rooms.Add(new RoomItem
                {
                    RoomId        = (int)reader["roomId"],
                    RoomNumber    = reader["roomNumber"].ToString()!,
                    Type          = reader["type"].ToString()!,
                    PricePerNight = (decimal)reader["pricePerNight"],
                    Capacity      = (int)reader["capacity"],
                    IsAvailable   = isAvailable,
                    Description   = reader["description"]?.ToString() ?? "",
                    DisplayStatus = status
                });
            }
        }
    }
}
