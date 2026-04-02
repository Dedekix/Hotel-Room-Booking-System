using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace HotelBookingSystem.Pages
{
    public class SignupModel : PageModel
    {
        string connectionString = "Data Source=DELPHINE\\SQLEXPRESS;Initial Catalog=HotelBookingSystemDB;Integrated Security=True;Trust Server Certificate=True";

        public string ErrorMessage { get; set; }

        [BindProperty]
        public string FullName { get; set; }

        [BindProperty]
        public string Email { get; set; }

        [BindProperty]
        public string Role { get; set; }

        public void OnGet()
        {
            // Redirect if already logged in
            if (!string.IsNullOrEmpty(HttpContext.Session.GetString("UserEmail")))
            {
                Response.Redirect("/Index");
            }
        }

        public IActionResult OnPost()
        {
            try
            {
                // Validate all fields
                if (string.IsNullOrEmpty(FullName) || string.IsNullOrEmpty(Email) || string.IsNullOrEmpty(Role))
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

                    // Check if email already exists
                    string checkQuery = "SELECT Id FROM Users WHERE Email = @email";
                    using (SqlCommand checkCmd = new SqlCommand(checkQuery, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@email", Email);
                        var existingUser = checkCmd.ExecuteScalar();

                        if (existingUser != null)
                        {
                            ErrorMessage = "An account with this email already exists. Please sign in instead.";
                            return Page();
                        }
                    }

                    // Create new user
                    string insertQuery = "INSERT INTO Users (FullName, Email, Role, CreatedAt, LastLoginAt) VALUES (@fullName, @email, @role, GETDATE(), GETDATE());";

                    using (SqlCommand insertCmd = new SqlCommand(insertQuery, conn))
                    {
                        insertCmd.Parameters.AddWithValue("@fullName", FullName);
                        insertCmd.Parameters.AddWithValue("@email", Email);
                        insertCmd.Parameters.AddWithValue("@role", Role);

                        insertCmd.ExecuteNonQuery();
                    }

                    conn.Close();
                }

                return RedirectToPage("/Login");
            }
            catch (Exception ex)
            {
                ErrorMessage = "An error occurred: " + ex.Message;
                return Page();
            }
        }
    }
}
