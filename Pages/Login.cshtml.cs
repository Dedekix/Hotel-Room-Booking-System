using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace HotelBookingSystem.Pages
{
    public class LoginModel : PageModel
    {
        string connectionString = "Data Source=Delphine\\SQLEXPRESS;Initial Catalog=HotelBookingDB;Integrated Security=True;Trust Server Certificate=True";

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

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    string query = "SELECT Id, FullName, Email, Role FROM Users WHERE Email = @Email AND IsActive = 1";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@Email", Email);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                HttpContext.Session.SetString("UserId", reader["Id"].ToString()!);
                                HttpContext.Session.SetString("UserFullName", reader["FullName"].ToString()!);
                                HttpContext.Session.SetString("UserEmail", reader["Email"].ToString()!);
                                HttpContext.Session.SetString("UserRole", reader["Role"].ToString()!);
                            }
                            else
                            {
                                ErrorMessage = "No account found with that email.";
                                return Page();
                            }
                        }
                    }

                    string updateLogin = "UPDATE Users SET LastLoginAt = GETDATE() WHERE Email = @Email";
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

            return RedirectToPage("/Index");
        }
    }
}
