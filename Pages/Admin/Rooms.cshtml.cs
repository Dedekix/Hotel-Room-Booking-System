using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace HotelBookingSystem.Pages.Admin
{
    public class RoomItem
    {
        public int     RoomId        { get; set; }
        public string  RoomNumber    { get; set; } = "";
        public string  Type          { get; set; } = "";
        public decimal PricePerNight { get; set; }
        public int     Capacity      { get; set; }
        public bool    IsAvailable   { get; set; }
        public bool    IsOccupied    { get; set; }
    }

    public class RoomsModel : PageModel
    {
        private readonly string _conn;
        public RoomsModel(string connectionString) => _conn = connectionString;

        public List<RoomItem> Rooms   { get; set; } = new();
        public string         Message { get; set; } = "";
        public bool           IsError { get; set; }

        public void OnGet()
        {
            if (!IsAdmin()) return;
            LoadRooms();
        }

        public IActionResult OnPost(int roomId, bool isAvailable)
        {
            if (!IsAdmin()) return RedirectToPage("/Login");

            using var conn = new SqlConnection(_conn);
            conn.Open();
            using var cmd = new SqlCommand("UPDATE Rooms SET isAvailable = @v WHERE roomId = @id", conn);
            cmd.Parameters.AddWithValue("@v",  isAvailable);
            cmd.Parameters.AddWithValue("@id", roomId);
            cmd.ExecuteNonQuery();

            Message = "Room status updated.";
            LoadRooms();
            return Page();
        }

        private void LoadRooms()
        {
            using var conn = new SqlConnection(_conn);
            conn.Open();
            string sql = @"SELECT r.roomId, r.roomNumber, r.type, r.pricePerNight, r.capacity, r.isAvailable,
                       (SELECT COUNT(*) FROM Bookings b WHERE b.roomId = r.roomId
                        AND b.status IN ('CONFIRMED','CHECKED_IN')) AS occupiedCount
                FROM Rooms r ORDER BY r.roomNumber";
            using var cmd    = new SqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                Rooms.Add(new RoomItem
                {
                    RoomId        = (int)reader["roomId"],
                    RoomNumber    = reader["roomNumber"].ToString()!,
                    Type          = reader["type"].ToString()!,
                    PricePerNight = (decimal)reader["pricePerNight"],
                    Capacity      = (int)reader["capacity"],
                    IsAvailable   = (bool)reader["isAvailable"],
                    IsOccupied    = (int)reader["occupiedCount"] > 0
                });
        }

        private bool IsAdmin()
        {
            var role = HttpContext.Session.GetString("UserRole");
            if (role != "ADMIN") { Response.Redirect("/Login"); return false; }
            return true;
        }
    }
}
