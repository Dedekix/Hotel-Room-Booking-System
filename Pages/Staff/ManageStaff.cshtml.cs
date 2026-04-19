using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace HotelBookingSystem.Pages.Staff
{
    public class StaffMember
    {
        public int Id { get; set; }
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public string Role { get; set; } = "";
    }

    public class ManageStaffModel : PageModel
    {
        private readonly string _conn;
        public ManageStaffModel(string connectionString) => _conn = connectionString;

        public List<StaffMember> StaffList { get; set; } = new();
        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }

        public void OnGet()
        {
            if (HttpContext.Session.GetString("UserRole") != "ADMIN")
            { Response.Redirect("/Login"); return; }
            LoadStaff();
        }

        public IActionResult OnPostAdd(string fullName, string email, string role)
        {
            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(role))
            { ErrorMessage = "All fields are required."; LoadStaff(); return Page(); }

            using var conn = new SqlConnection(_conn);
            conn.Open();

            using (var check = new SqlCommand("SELECT COUNT(*) FROM Users WHERE email = @e", conn))
            {
                check.Parameters.AddWithValue("@e", email);
                if ((int)check.ExecuteScalar() > 0)
                { ErrorMessage = "An account with this email already exists."; LoadStaff(); return Page(); }
            }

            using var cmd = new SqlCommand("INSERT INTO Users (fullName, email, role) VALUES (@n, @e, @r)", conn);
            cmd.Parameters.AddWithValue("@n", fullName);
            cmd.Parameters.AddWithValue("@e", email);
            cmd.Parameters.AddWithValue("@r", role.ToUpper());
            cmd.ExecuteNonQuery();

            SuccessMessage = $"{fullName} added successfully.";
            LoadStaff();
            return Page();
        }

        public IActionResult OnPostEdit(int staffId, string fullName, string email, string role)
        {
            using var conn = new SqlConnection(_conn);
            conn.Open();

            using (var check = new SqlCommand("SELECT COUNT(*) FROM Users WHERE email = @e AND userId != @id", conn))
            {
                check.Parameters.AddWithValue("@e", email);
                check.Parameters.AddWithValue("@id", staffId);
                if ((int)check.ExecuteScalar() > 0)
                { ErrorMessage = "That email is already in use by another account."; LoadStaff(); return Page(); }
            }

            using var cmd = new SqlCommand("UPDATE Users SET fullName=@n, email=@e, role=@r WHERE userId=@id", conn);
            cmd.Parameters.AddWithValue("@n", fullName);
            cmd.Parameters.AddWithValue("@e", email);
            cmd.Parameters.AddWithValue("@r", role.ToUpper());
            cmd.Parameters.AddWithValue("@id", staffId);
            cmd.ExecuteNonQuery();

            SuccessMessage = "Staff account updated.";
            LoadStaff();
            return Page();
        }

        public IActionResult OnPostDelete(int staffId)
        {
            // Prevent self-deletion
            var sessionId = HttpContext.Session.GetString("UserId");
            if (sessionId == staffId.ToString())
            { ErrorMessage = "You cannot delete your own account."; LoadStaff(); return Page(); }

            using var conn = new SqlConnection(_conn);
            conn.Open();
            using var cmd = new SqlCommand("DELETE FROM Users WHERE userId=@id AND role IN ('STAFF','ADMIN')", conn);
            cmd.Parameters.AddWithValue("@id", staffId);
            cmd.ExecuteNonQuery();

            SuccessMessage = "Staff account deleted.";
            LoadStaff();
            return Page();
        }

        private void LoadStaff()
        {
            StaffList.Clear();
            using var conn = new SqlConnection(_conn);
            conn.Open();
            using var cmd = new SqlCommand("SELECT userId, fullName, email, role FROM Users WHERE role IN ('STAFF','ADMIN') ORDER BY fullName", conn);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                StaffList.Add(new StaffMember
                {
                    Id       = (int)r["userId"],
                    FullName = r["fullName"].ToString()!,
                    Email    = r["email"].ToString()!,
                    Role     = r["role"].ToString()!
                });
        }
    }
}
