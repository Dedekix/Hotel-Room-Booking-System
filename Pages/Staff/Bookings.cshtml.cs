using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace HotelBookingSystem.Pages.Staff
{
    public class BookingItem
    {
        public int BookingId { get; set; }
        public string GuestName { get; set; } = "";
        public string RoomNumber { get; set; } = "";
        public DateTime CheckIn { get; set; }
        public DateTime CheckOut { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = "";
    }

    public class BookingsModel : PageModel
    {
        private readonly string _conn;
        public BookingsModel(string connectionString) => _conn = connectionString;

        public List<BookingItem> Bookings { get; set; } = new();

        public void OnGet()
        {
            var role = HttpContext.Session.GetString("UserRole");
            if (role != "STAFF" && role != "ADMIN") { Response.Redirect("/Login?returnUrl=/Staff/Bookings"); return; }
            LoadBookings();
        }

        public IActionResult OnPostCheckIn(int bookingId)
        {
            UpdateStatus(bookingId, "CHECKED_IN");
            return RedirectToPage();
        }

        public IActionResult OnPostCheckOut(int bookingId)
        {
            UpdateStatus(bookingId, "CHECKED_OUT");
            return RedirectToPage();
        }

        private void UpdateStatus(int bookingId, string status)
        {
            using var conn = new SqlConnection(_conn);
            conn.Open();
            using var cmd = new SqlCommand(
                "UPDATE Bookings SET status = @s WHERE bookingId = @id", conn);
            cmd.Parameters.AddWithValue("@s", status);
            cmd.Parameters.AddWithValue("@id", bookingId);
            cmd.ExecuteNonQuery();
        }

        private void LoadBookings()
        {
            Bookings.Clear();
            using var conn = new SqlConnection(_conn);
            conn.Open();

            string sql = @"
                SELECT b.bookingId, u.fullName, r.roomNumber,
                       b.checkInDate, b.checkOutDate, b.totalPrice, b.status
                FROM Bookings b
                JOIN Users u ON b.userId = u.userId
                JOIN Rooms r ON b.roomId = r.roomId
                ORDER BY b.bookingId DESC";

            using var cmd = new SqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                Bookings.Add(new BookingItem
                {
                    BookingId   = (int)reader["bookingId"],
                    GuestName   = reader["fullName"].ToString()!,
                    RoomNumber  = reader["roomNumber"].ToString()!,
                    CheckIn     = (DateTime)reader["checkInDate"],
                    CheckOut    = (DateTime)reader["checkOutDate"],
                    TotalAmount = (decimal)reader["totalPrice"],
                    Status      = reader["status"].ToString()!
                });
            }
        }
    }
}
