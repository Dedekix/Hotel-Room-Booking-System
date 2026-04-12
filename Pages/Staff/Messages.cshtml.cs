using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace HotelBookingSystem.Pages.Staff
{
    public class MessageItem
    {
        public int    MessageId { get; set; }
        public string FullName  { get; set; } = "";
        public string Email     { get; set; } = "";
        public string Subject   { get; set; } = "";
        public string Message   { get; set; } = "";
        public string SentAt    { get; set; } = "";
        public bool   IsRead    { get; set; }
    }

    public class MessagesModel : PageModel
    {
        private readonly string _conn;
        public MessagesModel(string connectionString) => _conn = connectionString;

        public List<MessageItem> Messages { get; set; } = new();

        public void OnGet()
        {
            if (HttpContext.Session.GetString("UserRole") != "ADMIN") { Response.Redirect("/Login"); return; }
            LoadMessages();
        }

        public IActionResult OnPost(int messageId)
        {
            if (HttpContext.Session.GetString("UserRole") != "ADMIN") return RedirectToPage("/Login");

            using var conn = new SqlConnection(_conn);
            conn.Open();
            using var cmd = new SqlCommand("UPDATE ContactMessages SET isRead = 1 WHERE messageId = @id", conn);
            cmd.Parameters.AddWithValue("@id", messageId);
            cmd.ExecuteNonQuery();

            LoadMessages();
            return Page();
        }

        private void LoadMessages()
        {
            using var conn = new SqlConnection(_conn);
            conn.Open();
            using var cmd    = new SqlCommand("SELECT messageId, fullName, email, subject, message, sentAt, isRead FROM ContactMessages ORDER BY sentAt DESC", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                Messages.Add(new MessageItem
                {
                    MessageId = (int)reader["messageId"],
                    FullName  = reader["fullName"].ToString()!,
                    Email     = reader["email"].ToString()!,
                    Subject   = reader["subject"].ToString()!,
                    Message   = reader["message"].ToString()!,
                    SentAt    = ((DateTime)reader["sentAt"]).ToString("MMM d, yyyy h:mm tt"),
                    IsRead    = (bool)reader["isRead"]
                });
        }
    }
}
