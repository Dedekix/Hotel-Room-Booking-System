using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace HotelBookingSystem.Pages.Customer
{
    public class PaymentModel : PageModel
    {
        private readonly string _conn;
        public PaymentModel(string connectionString) => _conn = connectionString;

        // What we're paying for
        public string  BookingType    { get; set; } = ""; // "ROOM" or "EVENT"
        public int     BookingId      { get; set; }
        public string  Summary        { get; set; } = "";
        public decimal Amount         { get; set; }
        public string? ErrorMessage   { get; set; }

        public IActionResult OnGet(string type, int id)
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserId")))
                return Redirect("/Login");

            BookingType = type.ToUpper();
            BookingId   = id;

            if (!LoadSummary()) return RedirectToPage("/Index");
            return Page();
        }

        public IActionResult OnPost(string type, int id, string method)
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserId")))
                return Redirect("/Login");

            BookingType = type.ToUpper();
            BookingId   = id;

            if (!LoadSummary())
            {
                ErrorMessage = "Booking not found.";
                return Page();
            }

            if (string.IsNullOrWhiteSpace(method))
            {
                ErrorMessage = "Please select a payment method.";
                return Page();
            }

            using var conn = new SqlConnection(_conn);
            conn.Open();

            bool isInPerson = method == "In Person";

            if (BookingType == "ROOM")
            {
                using var cmd = new SqlCommand(@"
                    INSERT INTO Payments (bookingId, amount, method, status)
                    VALUES (@bid, @amt, @method, @pstatus)", conn);
                cmd.Parameters.AddWithValue("@bid",     BookingId);
                cmd.Parameters.AddWithValue("@amt",     Amount);
                cmd.Parameters.AddWithValue("@method",  method);
                cmd.Parameters.AddWithValue("@pstatus", isInPerson ? "PENDING" : "COMPLETED");
                cmd.ExecuteNonQuery();

                if (!isInPerson)
                {
                    using var upd = new SqlCommand(
                        "UPDATE Bookings SET status = 'CONFIRMED' WHERE bookingId = @id", conn);
                    upd.Parameters.AddWithValue("@id", BookingId);
                    upd.ExecuteNonQuery();
                }
            }
            else
            {
                using var cmd = new SqlCommand(@"
                    INSERT INTO Payments (bookingId, eventBookingId, amount, method, status)
                    VALUES (NULL, @eid, @amt, @method, @pstatus)", conn);
                cmd.Parameters.AddWithValue("@eid",     BookingId);
                cmd.Parameters.AddWithValue("@amt",     Amount);
                cmd.Parameters.AddWithValue("@method",  method);
                cmd.Parameters.AddWithValue("@pstatus", isInPerson ? "PENDING" : "COMPLETED");
                cmd.ExecuteNonQuery();

                if (!isInPerson)
                {
                    using var upd = new SqlCommand(
                        "UPDATE EventBookings SET status = 'CONFIRMED' WHERE eventBookingId = @id", conn);
                    upd.Parameters.AddWithValue("@id", BookingId);
                    upd.ExecuteNonQuery();
                }
            }

            return RedirectToPage("/Customer/BookingConfirmed", new { type, id, paid = !isInPerson });
        }

        private bool LoadSummary()
        {
            using var conn = new SqlConnection(_conn);
            conn.Open();

            if (BookingType == "ROOM")
            {
                using var cmd = new SqlCommand(@"
                    SELECT r.roomNumber, r.type, b.checkInDate, b.checkOutDate, b.totalPrice
                    FROM Bookings b
                    JOIN Rooms r ON b.roomId = r.roomId
                    WHERE b.bookingId = @id AND b.userId = @uid", conn);
                cmd.Parameters.AddWithValue("@id",  BookingId);
                cmd.Parameters.AddWithValue("@uid", int.Parse(HttpContext.Session.GetString("UserId")!));
                using var r = cmd.ExecuteReader();
                if (!r.Read()) return false;
                Amount  = (decimal)r["totalPrice"];
                Summary = $"Room {r["roomNumber"]} ({r["type"]}) — " +
                          $"{((DateTime)r["checkInDate"]):MMM d} to {((DateTime)r["checkOutDate"]):MMM d, yyyy}";
            }
            else
            {
                using var cmd = new SqlCommand(@"
                    SELECT e.title, e.eventDate, e.price
                    FROM EventBookings eb
                    JOIN Events e ON eb.eventId = e.eventId
                    WHERE eb.eventBookingId = @id AND eb.userId = @uid", conn);
                cmd.Parameters.AddWithValue("@id",  BookingId);
                cmd.Parameters.AddWithValue("@uid", int.Parse(HttpContext.Session.GetString("UserId")!));
                using var r = cmd.ExecuteReader();
                if (!r.Read()) return false;
                Amount  = (decimal)r["price"];
                Summary = $"{r["title"]} — {((DateTime)r["eventDate"]):MMM d, yyyy}";
            }

            return true;
        }
    }
}
