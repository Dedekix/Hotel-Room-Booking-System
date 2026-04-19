using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace HotelBookingSystem.Pages.Customer
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

        public string LoggedInName { get; set; } = "";

        public IActionResult OnGet(int roomId)
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserId")))
                return Redirect($"/Login?returnUrl=/Customer/BookRoom/{roomId}");
            if (!LoadRoom(roomId))
                return RedirectToPage("/Rooms");
            LoggedInName = HttpContext.Session.GetString("UserFullName") ?? "";
            return Page();
        }

        public IActionResult OnPost(
            int    roomId,
            string bookingType,
            string? checkInDate,
            string? checkOutDate,
            string? checkInTime,
            string? checkOutTime,
            string? hourlyDate,
            string? startTime,
            int?   durationHours,
            int    guestCount)
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserId")))
                return Redirect($"/Login?returnUrl=/Customer/BookRoom/{roomId}");

            LoadRoom(roomId);
            LoggedInName = HttpContext.Session.GetString("UserFullName") ?? "";

            if (guestCount < 1 || guestCount > Capacity)
            {
                ErrorMessage = $"Guest count must be between 1 and {Capacity}.";
                return Page();
            }

            int userId = int.Parse(HttpContext.Session.GetString("UserId")!);

            using var conn = new SqlConnection(_conn);
            conn.Open();

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
                checkIn    = hDate.Date;
                checkOut   = hDate.Date.AddDays(1);  // DATE column: next day satisfies CHK_Dates
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

            // ── Insert booking & redirect to payment ──────────
            string insert = @"
                INSERT INTO Bookings
                    (userId, roomId, checkInDate, checkOutDate, guestCount, totalPrice, status)
                OUTPUT INSERTED.bookingId
                VALUES
                    (@uid, @rid, @cin, @cout, @guests, @price, 'PENDING')";

            int newBookingId;
            using (var cmd = new SqlCommand(insert, conn))
            {
                cmd.Parameters.AddWithValue("@uid",    userId);
                cmd.Parameters.AddWithValue("@rid",    roomId);
                cmd.Parameters.AddWithValue("@cin",    checkIn.Date);
                cmd.Parameters.AddWithValue("@cout",   checkOut.Date);
                cmd.Parameters.AddWithValue("@guests", guestCount);
                cmd.Parameters.AddWithValue("@price",  totalPrice);
                newBookingId = (int)cmd.ExecuteScalar();
            }

            return RedirectToPage("/Customer/Payment", new { type = "ROOM", id = newBookingId });
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
                "SINGLE" => "Images/Single room.jpg",
                _        => "Images/room101.jpg"
            };
            return true;
        }
    }
}
