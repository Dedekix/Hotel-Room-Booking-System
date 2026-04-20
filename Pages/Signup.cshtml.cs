using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace HotelBookingSystem.Pages
{
    public class User
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }

    public class SignupModel : PageModel
    {
        private readonly string _connectionString;

        public SignupModel(string connectionString)
        {
            _connectionString = connectionString;
        }

        [BindProperty]
        public string FullName { get; set; } = string.Empty;

        [BindProperty]
        public string Email { get; set; } = string.Empty;

        [BindProperty]
        public string Phone { get; set; } = string.Empty;

        public string? ErrorMessage { get; set; }

        public void OnGet() { }

        public IActionResult OnPost()
        {
            if (string.IsNullOrWhiteSpace(FullName) || string.IsNullOrWhiteSpace(Email))
            {
                ErrorMessage = "All fields are required.";
                return Page();
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    string checkQuery = "SELECT COUNT(*) FROM Users WHERE email = @Email";
                    using (SqlCommand checkCmd = new SqlCommand(checkQuery, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@Email", Email);
                        int count = (int)checkCmd.ExecuteScalar();
                        if (count > 0)
                        {
                            ErrorMessage = "An account with this email already exists.";
                            return Page();
                        }
                    }

                    string insertQuery = "INSERT INTO Users (fullName, email, phone, role) VALUES (@FullName, @Email, @Phone, @Role)";
                    using (SqlCommand cmd = new SqlCommand(insertQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@FullName", FullName);
                        cmd.Parameters.AddWithValue("@Email", Email);
                        cmd.Parameters.AddWithValue("@Phone", string.IsNullOrWhiteSpace(Phone) ? DBNull.Value : Phone);
                        cmd.Parameters.AddWithValue("@Role", "CUSTOMER");
                        cmd.ExecuteNonQuery();
                    }

                    conn.Close();
                }
            }
            catch (Exception)
            {
                ErrorMessage = "Something went wrong. Please try again.";
                return Page();
            }

            return RedirectToPage("/Login");
        }
    }
}
