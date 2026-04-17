using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace HotelBookingSystem.Pages.Customer
{
    public class VerifyOtpModel : PageModel
    {
        private readonly string _conn;
        public VerifyOtpModel(string connectionString) => _conn = connectionString;

        public string? ErrorMessage { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? ReturnUrl { get; set; }

        public IActionResult OnGet()
        {
            if (HttpContext.Session.GetString("OtpEmail") == null)
                return RedirectToPage("/Login");
            return Page();
        }

        public IActionResult OnPost(string otp)
        {
            var email      = HttpContext.Session.GetString("OtpEmail");
            var storedOtp  = HttpContext.Session.GetString("OtpCode");
            var expiryStr  = HttpContext.Session.GetString("OtpExpiry");

            if (email == null || storedOtp == null || expiryStr == null)
                return RedirectToPage("/Login");

            if (DateTime.UtcNow > DateTime.Parse(expiryStr))
            {
                ErrorMessage = "OTP has expired. Please request a new one.";
                return Page();
            }

            if (otp?.Trim() != storedOtp)
            {
                ErrorMessage = "Invalid OTP. Please try again.";
                return Page();
            }

            // OTP valid — complete login
            HttpContext.Session.Remove("OtpCode");
            HttpContext.Session.Remove("OtpExpiry");
            HttpContext.Session.Remove("OtpEmail");

            string role = "";
            using var conn = new SqlConnection(_conn);
            conn.Open();
            using (var cmd = new SqlCommand(
                "SELECT userId, fullName, email, role FROM Users WHERE email = @e AND isActive = 1", conn))
            {
                cmd.Parameters.AddWithValue("@e", email);
                using var r = cmd.ExecuteReader();
                if (!r.Read()) return RedirectToPage("/Login");
                role = r["role"].ToString()!;
                HttpContext.Session.SetString("UserId",       r["userId"].ToString()!);
                HttpContext.Session.SetString("UserFullName", r["fullName"].ToString()!);
                HttpContext.Session.SetString("UserEmail",    r["email"].ToString()!);
                HttpContext.Session.SetString("UserRole",     role);
            }
            using (var cmd = new SqlCommand("UPDATE Users SET lastLoginAt = GETDATE() WHERE email = @e", conn))
            {
                cmd.Parameters.AddWithValue("@e", email);
                cmd.ExecuteNonQuery();
            }

            if (role == "ADMIN" || role == "STAFF")
                return Redirect(!string.IsNullOrEmpty(ReturnUrl) ? ReturnUrl : "/Staff/Dashboard");
            return Redirect(!string.IsNullOrEmpty(ReturnUrl) ? ReturnUrl : "/Index");
        }
    }
}
