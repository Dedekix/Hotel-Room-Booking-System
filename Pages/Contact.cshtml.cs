using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace HotelBookingSystem.Pages
{
    public class ContactModel : PageModel
    {
        private readonly string _conn;
        public ContactModel(string connectionString) => _conn = connectionString;

        public bool   Sent         { get; set; }
        public string ErrorMessage { get; set; } = "";

        public void OnGet() { }

        public IActionResult OnPost(string fullName, string email, string subject, string message)
        {
            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(subject)  || string.IsNullOrWhiteSpace(message))
            {
                ErrorMessage = "All fields are required.";
                return Page();
            }

            try
            {
                using var conn = new SqlConnection(_conn);
                conn.Open();

                string sql = @"INSERT INTO ContactMessages (fullName, email, subject, message)
                               VALUES (@fullName, @email, @subject, @message)";

                using var cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@fullName", fullName);
                cmd.Parameters.AddWithValue("@email",    email);
                cmd.Parameters.AddWithValue("@subject",  subject);
                cmd.Parameters.AddWithValue("@message",  message);
                cmd.ExecuteNonQuery();

                Sent = true;
            }
            catch
            {
                ErrorMessage = "Something went wrong. Please try again.";
            }

            return Page();
        }
    }
}
