using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace HotelBookingSystem.Pages.Admin
{
    public class BookingItem
    {
        public int     BookingId   { get; set; }
        public string  GuestName   { get; set; } = "";
        public string  Email       { get; set; } = "";
        public string  RoomNumber  { get; set; } = "";
        public string  CheckIn     { get; set; } = "";
        public string  CheckOut    { get; set; } = "";
        public int     GuestCount  { get; set; }
        public decimal TotalPrice  { get; set; }
        public string  Status      { get; set; } = "";
    }

    public class BookingsModel : PageModel
    {
        private readonly string _conn;
        public BookingsModel(string connectionString) => _conn = connectionString;

        public List<BookingItem> Bookings { get; set; } = new();

        public void OnGet()
        {
            var role = HttpContext.Session.GetString("UserRole");
            if (role != "ADMIN") { Response.Redirect("/Login"); return; }

            using var conn = new SqlConnection(_conn);
            conn.Open();

            string sql = @"
                SELECT b.bookingId, u.fullName, u.email, r.roomNumber,
                       b.checkInDate, b.checkOutDate, b.guestCount, b.totalPrice, b.status
                FROM Bookings b
                JOIN Users u ON u.userId = b.userId
                JOIN Rooms  r ON r.roomId = b.roomId
                ORDER BY b.createdAt DESC";

            using var cmd    = new SqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                Bookings.Add(new BookingItem
                {
                    BookingId  = (int)reader["bookingId"],
                    GuestName  = reader["fullName"].ToString()!,
                    Email      = reader["email"].ToString()!,
                    RoomNumber = reader["roomNumber"].ToString()!,
                    CheckIn    = ((DateTime)reader["checkInDate"]).ToString("MMM d, yyyy"),
                    CheckOut   = ((DateTime)reader["checkOutDate"]).ToString("MMM d, yyyy"),
                    GuestCount = (int)reader["guestCount"],
                    TotalPrice = (decimal)reader["totalPrice"],
                    Status     = reader["status"].ToString()!
                });
        }
    }
}
