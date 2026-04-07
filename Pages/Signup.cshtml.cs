using System.Data;
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
        string connectionString = "Data Source=Delphine\\SQLEXPRESS;Initial Catalog=HotelBookingDB;Integrated Security=True;Trust Server Certificate=True";

        [BindProperty]
        public string FullName { get; set; } = string.Empty;

        [BindProperty]
        public string Email { get; set; } = string.Empty;

        [BindProperty]
        public string Role { get; set; } = "Customer";

        public string? ErrorMessage { get; set; }

        public void OnGet() { }

        public IActionResult OnPost()
        {
            if (string.IsNullOrWhiteSpace(FullName) || string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Role))
            {
                ErrorMessage = "All fields are required.";
                return Page();
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    string checkQuery = "SELECT COUNT(*) FROM Users WHERE Email = @Email";
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

                    string insertQuery = "INSERT INTO Users (FullName, Email, Role) VALUES (@FullName, @Email, @Role)";
                    using (SqlCommand cmd = new SqlCommand(insertQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@FullName", FullName);
                        cmd.Parameters.AddWithValue("@Email", Email);
                        cmd.Parameters.AddWithValue("@Role", Role);
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

            return RedirectToPage("/Login");
        }
    }
}
