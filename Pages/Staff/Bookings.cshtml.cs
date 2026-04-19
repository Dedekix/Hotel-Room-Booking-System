using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace HotelBookingSystem.Pages.Staff
{
    public class BookingItem
    {
        public int      BookingId   { get; set; }
        public string   GuestName   { get; set; } = "";
        public string   RoomNumber  { get; set; } = "";
        public DateTime CheckIn     { get; set; }
        public DateTime CheckOut    { get; set; }
        public decimal  TotalAmount { get; set; }
        public string   Status      { get; set; } = "";
    }

    public class BookingsModel : PageModel
    {
        private readonly string _conn;
        private const int PageSize = 5;
        public BookingsModel(string connectionString) => _conn = connectionString;

        public List<BookingItem> Bookings    { get; set; } = new();
        public int CurrentPage  { get; set; } = 1;
        public int TotalPages   { get; set; } = 1;
        public int TotalCount   { get; set; }

        public void OnGet(int p = 1)
        {
            var role = HttpContext.Session.GetString("UserRole");
            if (role != "STAFF" && role != "ADMIN") { Response.Redirect("/Login?returnUrl=/Staff/Bookings"); return; }
            CurrentPage = Math.Max(1, p);
            LoadBookings();
        }

        public IActionResult OnPostConfirm(int bookingId, int p = 1)
        {
            UpdateStatus(bookingId, "CONFIRMED");
            return RedirectToPage(new { p });
        }

        public IActionResult OnPostCheckIn(int bookingId, int p = 1)
        {
            UpdateStatus(bookingId, "CHECKED_IN");
            return RedirectToPage(new { p });
        }

        public IActionResult OnPostCheckOut(int bookingId, int p = 1)
        {
            UpdateStatus(bookingId, "CHECKED_OUT");
            return RedirectToPage(new { p });
        }

        private void UpdateStatus(int bookingId, string status)
        {
            using var conn = new SqlConnection(_conn);
            conn.Open();
            using var cmd = new SqlCommand("UPDATE Bookings SET status = @s WHERE bookingId = @id", conn);
            cmd.Parameters.AddWithValue("@s",  status);
            cmd.Parameters.AddWithValue("@id", bookingId);
            cmd.ExecuteNonQuery();
        }

        private void LoadBookings()
        {
            using var conn = new SqlConnection(_conn);
            conn.Open();

            using (var cmd = new SqlCommand("SELECT COUNT(*) FROM Bookings", conn))
                TotalCount = (int)cmd.ExecuteScalar();

            TotalPages  = Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
            CurrentPage = Math.Min(CurrentPage, TotalPages);

            string sql = @"
                SELECT b.bookingId, u.fullName, r.roomNumber,
                       b.checkInDate, b.checkOutDate, b.totalPrice, b.status
                FROM Bookings b
                JOIN Users u ON b.userId = u.userId
                JOIN Rooms r ON b.roomId = r.roomId
                ORDER BY b.bookingId DESC
                OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY";

            using var cmd2   = new SqlCommand(sql, conn);
            cmd2.Parameters.AddWithValue("@skip", (CurrentPage - 1) * PageSize);
            cmd2.Parameters.AddWithValue("@take", PageSize);
            using var reader = cmd2.ExecuteReader();
            while (reader.Read())
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
