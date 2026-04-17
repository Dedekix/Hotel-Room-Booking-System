using HotelBookingSystem.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace HotelBookingSystem.Pages
{
    public class LoginModel : PageModel
    {
        private readonly string _connectionString;
        private readonly OtpEmailService _otpService;

        public LoginModel(string connectionString, OtpEmailService otpService)
        {
            _connectionString = connectionString;
            _otpService       = otpService;
        }

        [BindProperty]
        public string Email { get; set; } = string.Empty;

        public string? ErrorMessage { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? ReturnUrl { get; set; }

        public void OnGet() { }

        public IActionResult OnPost()
        {
            if (string.IsNullOrWhiteSpace(Email))
            {
                ErrorMessage = "Please enter your email.";
                return Page();
            }

            string userId = "", fullName = "", role = "";

            try
            {
                using var conn = new SqlConnection(_connectionString);
                conn.Open();
                using var cmd = new SqlCommand(
                    "SELECT userId, fullName, role FROM Users WHERE email = @Email AND isActive = 1", conn);
                cmd.Parameters.AddWithValue("@Email", Email);
                using var reader = cmd.ExecuteReader();
                if (!reader.Read())
                {
                    ErrorMessage = "No account found with that email.";
                    return Page();
                }
                userId   = reader["userId"].ToString()!;
                fullName = reader["fullName"].ToString()!;
                role     = reader["role"].ToString()!;
            }
            catch
            {
                ErrorMessage = "Something went wrong. Please try again.";
                return Page();
            }

            // Staff and admins skip OTP — log in directly
            if (role == "STAFF" || role == "ADMIN")
            {
                HttpContext.Session.SetString("UserId",       userId);
                HttpContext.Session.SetString("UserFullName", fullName);
                HttpContext.Session.SetString("UserEmail",    Email);
                HttpContext.Session.SetString("UserRole",     role);

                using var conn = new SqlConnection(_connectionString);
                conn.Open();
                using var cmd = new SqlCommand("UPDATE Users SET lastLoginAt = GETDATE() WHERE email = @Email", conn);
                cmd.Parameters.AddWithValue("@Email", Email);
                cmd.ExecuteNonQuery();

                return Redirect(!string.IsNullOrEmpty(ReturnUrl) ? ReturnUrl : "/Staff/Dashboard");
            }

            // Customers go through OTP
            string otp = Random.Shared.Next(100000, 999999).ToString();

            HttpContext.Session.SetString("OtpEmail",  Email);
            HttpContext.Session.SetString("OtpCode",   otp);
            HttpContext.Session.SetString("OtpExpiry", DateTime.UtcNow.AddMinutes(5).ToString("o"));

            _otpService.PublishOtp(Email, otp);

            return RedirectToPage("/Customer/VerifyOtp", new { ReturnUrl });
        }
    }
}
