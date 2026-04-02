using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace HotelBookingSystem.Pages
{
    public class LoginModel : PageModel
    {
        string connectionString = "Data Source=DELPHINE\\SQLEXPRESS;Initial Catalog=HotelBookingSystemDB;Integrated Security=True;Trust Server Certificate=True";

        public string ErrorMessage { get; set; }
        
        [BindProperty]
        public string Email { get; set; }
        
        [BindProperty]
        public string Role { get; set; }

        public void OnGet()
        {
            // Check if user is already logged in
            if (!string.IsNullOrEmpty(HttpContext.Session.GetString("UserEmail")))
            {
                Response.Redirect("/Index");
            }
        }

        public IActionResult OnPost()
        {
            try
            {
                // Validate fields
                if (string.IsNullOrEmpty(Email) || string.IsNullOrEmpty(Role))
                {
                    ErrorMessage = "Please fill in all fields.";
                    return Page();
                }

                // Validate email format
                if (!Email.Contains("@") || !Email.Contains("."))
                {
                    ErrorMessage = "Please enter a valid email address.";
                    return Page();
                }

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    
                    // Check if user exists
                    string checkQuery = "SELECT Id, FullName, Email, Role FROM Users WHERE Email = @email";

                    using (SqlCommand cmd = new SqlCommand(checkQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@email", Email);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                // User exists
                                int userId = int.Parse(reader["Id"].ToString());
                                string fullName = reader["FullName"].ToString();
                                string existingRole = reader["Role"].ToString();
                                reader.Close();

                                // Verify the role matches
                                if (!existingRole.Equals(Role, StringComparison.OrdinalIgnoreCase))
                                {
                                    ErrorMessage = $"This account is registered as {existingRole}, not {Role}.";
                                    return Page();
                                }

                                // Update last login time
                                string updateQuery = "UPDATE Users SET LastLoginAt = GETDATE() WHERE Id = @userId";
                                using (SqlCommand updateCmd = new SqlCommand(updateQuery, conn))
                                {
                                    updateCmd.Parameters.AddWithValue("@userId", userId);
                                    updateCmd.ExecuteNonQuery();
                                }

                                // Set session
                                HttpContext.Session.SetString("UserId", userId.ToString());
                                HttpContext.Session.SetString("UserFullName", fullName);
                                HttpContext.Session.SetString("UserEmail", Email);
                                HttpContext.Session.SetString("UserRole", existingRole);
                            }
                            else
                            {
                                reader.Close();
                                ErrorMessage = "No account found with this email. Please sign up first.";
                                return Page();
                            }
                        }
                    }
                    conn.Close();
                }

                return RedirectToPage("/Index");
            }
            catch (Exception ex)
            {
                ErrorMessage = "An error occurred: " + ex.Message;
                return Page();
            }
        }
    }
}