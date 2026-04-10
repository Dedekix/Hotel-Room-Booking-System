using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace HotelBookingSystem.Pages
{
    public class LoginModel : PageModel
    {
        private readonly string _connectionString;

        public LoginModel(string connectionString)
        {
            _connectionString = connectionString;
        }

        [BindProperty]
        public string Email { get; set; } = string.Empty;

        public string? ErrorMessage { get; set; }

        public void OnGet() { }

        public IActionResult OnPost()
        {
            if (string.IsNullOrWhiteSpace(Email))
            {
                ErrorMessage = "Please enter your email.";
                return Page();
            }

            string role = "";
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                {
                    conn.Open();

                    string query = "SELECT userId, fullName, email, role FROM Users WHERE email = @Email AND isActive = 1";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Email", Email);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                role = reader["role"].ToString()!;
                                HttpContext.Session.SetString("UserId", reader["userId"].ToString()!);
                                HttpContext.Session.SetString("UserFullName", reader["fullName"].ToString()!);
                                HttpContext.Session.SetString("UserEmail", reader["email"].ToString()!);
                                HttpContext.Session.SetString("UserRole", role);
                            }
                            else
                            {
                                ErrorMessage = "No account found with that email.";
                                return Page();
                            }
                        }
                    }

                    string updateLogin = "UPDATE Users SET lastLoginAt = GETDATE() WHERE email = @Email";
                    using (SqlCommand cmd = new SqlCommand(updateLogin, conn))
                    {
                        cmd.Parameters.AddWithValue("@Email", Email);
                        cmd.ExecuteNonQuery();
                    }

                    conn.Close();
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Something went wrong. Please try again.";
                return Page();
            }

            return role == "ADMIN" ? RedirectToPage("/Admin/Dashboard") : RedirectToPage("/Index");
        }
    }
}
