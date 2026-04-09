using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace HotelBookingSystem.Pages
{
    public class BookRoomModel : PageModel
    {
        private readonly string _conn;
        public BookRoomModel(string connectionString) => _conn = connectionString;

        // Room data loaded from DB
        public int     RoomId        { get; set; }
        public string  RoomNumber    { get; set; } = "";
        public string  RoomType      { get; set; } = "";
        public decimal PricePerNight { get; set; }
        public int     Capacity      { get; set; }
        public string  Description   { get; set; } = "";
        public string  ImagePath     { get; set; } = "";

        public string? ErrorMessage   { get; set; }
        public string? SuccessMessage { get; set; }

        public IActionResult OnGet(int roomId)
        {
            if (!LoadRoom(roomId))
                return RedirectToPage("/Rooms");
            return Page();
        }

        public IActionResult OnPost(
            int    roomId,
            string bookingType,
            string fullName,
            string email,
            string? checkInDate,
            string? checkOutDate,
            string? checkInTime,
            string? checkOutTime,
            string? hourlyDate,
            string? startTime,
            int?   durationHours,
            int    guestCount)
        {
            LoadRoom(roomId);

            // ── Validate ──────────────────────────────────────
            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email))
            {
                ErrorMessage = "Full name and email are required.";
                return Page();
            }

            if (guestCount < 1 || guestCount > Capacity)
            {
                ErrorMessage = $"Guest count must be between 1 and {Capacity}.";
                return Page();
            }

            using var conn = new SqlConnection(_conn);
            conn.Open();

            // ── Resolve userId from email ─────────────────────
            int userId;
            using (var cmd = new SqlCommand(
                "SELECT userId FROM Users WHERE email = @e AND isActive = 1", conn))
            {
                cmd.Parameters.AddWithValue("@e", email);
                var result = cmd.ExecuteScalar();
                if (result == null)
                {
                    ErrorMessage = "No active account found for that email. Please sign up first.";
                    return Page();
                }
                userId = (int)result;
            }

            // ── Calculate dates & price ───────────────────────
            DateTime checkIn, checkOut;
            decimal  totalPrice;

            if (bookingType == "HOURLY")
            {
                if (!DateTime.TryParse(hourlyDate, out var hDate) || string.IsNullOrEmpty(startTime))
                {
                    ErrorMessage = "Please select a date and start time.";
                    return Page();
                }
                int hours = durationHours ?? 1;
                checkIn    = hDate;
                checkOut   = hDate;
                // pricePerHour not in DB schema — derive as pricePerNight / 8
                totalPrice = Math.Round(PricePerNight / 8 * hours, 2);
            }
            else
            {
                if (!DateTime.TryParse(checkInDate, out checkIn) ||
                    !DateTime.TryParse(checkOutDate, out checkOut) ||
                    checkOut <= checkIn)
                {
                    ErrorMessage = "Please select valid check-in and check-out dates.";
                    return Page();
                }
                int nights = (checkOut - checkIn).Days;
                totalPrice = PricePerNight * nights;
            }

            // ── Double-booking check ──────────────────────────
            string overlapSql = @"
                SELECT COUNT(*) FROM Bookings
                WHERE roomId = @rid
                  AND status NOT IN ('CANCELLED','CHECKED_OUT')
                  AND checkInDate  < @cout
                  AND checkOutDate > @cin";

            using (var cmd = new SqlCommand(overlapSql, conn))
            {
                cmd.Parameters.AddWithValue("@rid",  roomId);
                cmd.Parameters.AddWithValue("@cin",  checkIn.Date);
                cmd.Parameters.AddWithValue("@cout", checkOut.Date);
                if ((int)cmd.ExecuteScalar() > 0)
                {
                    ErrorMessage = "This room is already booked for the selected dates.";
                    return Page();
                }
            }

            // ── Insert booking ────────────────────────────────
            string insert = @"
                INSERT INTO Bookings
                    (userId, roomId, checkInDate, checkOutDate, guestCount, totalPrice, status)
                VALUES
                    (@uid, @rid, @cin, @cout, @guests, @price, 'CONFIRMED')";

            using (var cmd = new SqlCommand(insert, conn))
            {
                cmd.Parameters.AddWithValue("@uid",    userId);
                cmd.Parameters.AddWithValue("@rid",    roomId);
                cmd.Parameters.AddWithValue("@cin",    checkIn.Date);
                cmd.Parameters.AddWithValue("@cout",   checkOut.Date);
                cmd.Parameters.AddWithValue("@guests", guestCount);
                cmd.Parameters.AddWithValue("@price",  totalPrice);
                cmd.ExecuteNonQuery();
            }

            return RedirectToPage("/BookingConfirmed");
        }

        private bool LoadRoom(int roomId)
        {
            using var conn = new SqlConnection(_conn);
            conn.Open();
            using var cmd = new SqlCommand(
                "SELECT roomId, roomNumber, type, pricePerNight, capacity, description FROM Rooms WHERE roomId = @id AND isAvailable = 1",
                conn);
            cmd.Parameters.AddWithValue("@id", roomId);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return false;

            RoomId        = (int)reader["roomId"];
            RoomNumber    = reader["roomNumber"].ToString()!;
            RoomType      = reader["type"].ToString()!;
            PricePerNight = (decimal)reader["pricePerNight"];
            Capacity      = (int)reader["capacity"];
            Description   = reader["description"]?.ToString() ?? "";
            ImagePath     = RoomType.ToUpper() switch
            {
                "SUITE"  => "Images/room301.jpg",
                "DELUXE" => "Images/room201.jpg",
                _        => "Images/room101.jpg"
            };
            return true;
        }
    }
}
