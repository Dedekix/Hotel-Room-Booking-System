using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace HotelBookingSystem.Pages.Staff
{
    public class DashboardActivity
    {
        public int BookingId { get; set; }
        public string GuestName { get; set; } = "";
        public string RoomNumber { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTime CheckIn { get; set; }
        public DateTime CheckOut { get; set; }
    }

    public class DashboardModel : PageModel
    {
        private readonly string _conn;
        public DashboardModel(string connectionString) => _conn = connectionString;

        public int TotalBookings { get; set; }
        public int TodayCheckIns { get; set; }
        public decimal TotalRevenue { get; set; }
        public int OccupancyPercent { get; set; }
        public int TotalRooms { get; set; }
        public int PendingMessages { get; set; }
        public List<DashboardActivity> TodayActivity { get; set; } = new();

        public IActionResult OnGet()
        {
            var role = HttpContext.Session.GetString("UserRole");
            if (role != "STAFF" && role != "ADMIN")
                return Redirect("/Login?returnUrl=/Staff/Dashboard");

            using var conn = new SqlConnection(_conn);
            conn.Open();

            TotalBookings = Scalar<int>(conn,
                "SELECT COUNT(*) FROM Bookings WHERE status NOT IN ('CANCELLED')");

            TodayCheckIns = Scalar<int>(conn,
                "SELECT COUNT(*) FROM Bookings WHERE status = 'CHECKED_IN'");

            TotalRevenue = Scalar<decimal>(conn,
                "SELECT ISNULL(SUM(totalPrice),0) FROM Bookings WHERE status IN ('CONFIRMED','CHECKED_IN','CHECKED_OUT')");

            TotalRooms = Scalar<int>(conn, "SELECT COUNT(*) FROM Rooms");

            int occupiedRooms = Scalar<int>(conn,
                "SELECT COUNT(*) FROM Bookings WHERE status = 'CHECKED_IN'");
            OccupancyPercent = TotalRooms > 0 ? (int)Math.Round(occupiedRooms * 100.0 / TotalRooms) : 0;

            PendingMessages = Scalar<int>(conn,
                "SELECT COUNT(*) FROM ContactMessages WHERE isRead = 0");

            // Today's active guests (checked-in) + today's arrivals (confirmed, check-in = today)
            string sql = @"
                SELECT b.bookingId, u.fullName, r.roomNumber, b.status, b.checkInDate, b.checkOutDate
                FROM Bookings b
                JOIN Users u ON b.userId = u.userId
                JOIN Rooms r ON b.roomId = r.roomId
                WHERE b.status IN ('CHECKED_IN', 'CONFIRMED')
                ORDER BY b.checkInDate ASC";

            using var cmd = new SqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                TodayActivity.Add(new DashboardActivity
                {
                    BookingId  = (int)reader["bookingId"],
                    GuestName  = reader["fullName"].ToString()!,
                    RoomNumber = reader["roomNumber"].ToString()!,
                    Status     = reader["status"].ToString()!,
                    CheckIn    = (DateTime)reader["checkInDate"],
                    CheckOut   = (DateTime)reader["checkOutDate"]
                });
            }

            return Page();
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
            using var cmd = new SqlCommand("UPDATE Bookings SET status = @s WHERE bookingId = @id", conn);
            cmd.Parameters.AddWithValue("@s", status);
            cmd.Parameters.AddWithValue("@id", bookingId);
            cmd.ExecuteNonQuery();
        }

        private static T Scalar<T>(SqlConnection conn, string sql)
        {
            using var cmd = new SqlCommand(sql, conn);
            var result = cmd.ExecuteScalar();
            return result == null || result == DBNull.Value ? default! : (T)Convert.ChangeType(result, typeof(T));
        }
    }
}
