using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace HotelBookingSystem.Pages.Admin
{
    public class DashboardModel : PageModel
    {
        private readonly string _conn;
        public DashboardModel(string connectionString) => _conn = connectionString;

        public int     TotalBookings    { get; set; }
        public int     TotalRooms       { get; set; }
        public int     TotalMessages    { get; set; }
        public int     UnreadMessages   { get; set; }
        public int     TotalEvents      { get; set; }
        public decimal TotalRevenue     { get; set; }

        public void OnGet()
        {
            var userId = HttpContext.Session.GetString("UserId");
            var role   = HttpContext.Session.GetString("UserRole");
            if (userId == null || role != "ADMIN") { Response.Redirect("/Login"); return; }

            using var conn = new SqlConnection(_conn);
            conn.Open();

            string sql = @"
                SELECT
                    (SELECT COUNT(*)                                FROM Bookings)                              AS totalBookings,
                    (SELECT COUNT(*)                                FROM Rooms)                                 AS totalRooms,
                    (SELECT COUNT(*)                                FROM ContactMessages)                       AS totalMessages,
                    (SELECT COUNT(*)                                FROM ContactMessages WHERE isRead = 0)      AS unreadMessages,
                    (SELECT COUNT(*)                                FROM Events)                                AS totalEvents,
                    (SELECT ISNULL(SUM(totalPrice),0)               FROM Bookings WHERE status != 'CANCELLED')  AS totalRevenue";

            using var cmd    = new SqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                TotalBookings  = (int)reader["totalBookings"];
                TotalRooms     = (int)reader["totalRooms"];
                TotalMessages  = (int)reader["totalMessages"];
                UnreadMessages = (int)reader["unreadMessages"];
                TotalEvents    = (int)reader["totalEvents"];
                TotalRevenue   = (decimal)reader["totalRevenue"];
            }
        }

    }
}
