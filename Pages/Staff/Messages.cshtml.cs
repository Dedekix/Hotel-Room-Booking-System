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
        private const int PageSize = 5;
        public MessagesModel(string connectionString) => _conn = connectionString;

        public List<MessageItem> Messages   { get; set; } = new();
        public int CurrentPage { get; set; } = 1;
        public int TotalPages  { get; set; } = 1;
        public int TotalCount  { get; set; }

        public void OnGet(int p = 1)
        {
            if (HttpContext.Session.GetString("UserRole") != "ADMIN") { Response.Redirect("/Login"); return; }
            CurrentPage = Math.Max(1, p);
            LoadMessages();
        }

        public IActionResult OnPost(int messageId, int p = 1)
        {
            if (HttpContext.Session.GetString("UserRole") != "ADMIN") return RedirectToPage("/Login");

            using var conn = new SqlConnection(_conn);
            conn.Open();
            using var cmd = new SqlCommand("UPDATE ContactMessages SET isRead = 1 WHERE messageId = @id", conn);
            cmd.Parameters.AddWithValue("@id", messageId);
            cmd.ExecuteNonQuery();

            return RedirectToPage(new { p });
        }

        private void LoadMessages()
        {
            using var conn = new SqlConnection(_conn);
            conn.Open();

            using (var cmd = new SqlCommand("SELECT COUNT(*) FROM ContactMessages", conn))
                TotalCount = (int)cmd.ExecuteScalar();

            TotalPages  = Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
            CurrentPage = Math.Min(CurrentPage, TotalPages);

            string sql = @"
                SELECT messageId, fullName, email, subject, message, sentAt, isRead
                FROM ContactMessages
                ORDER BY sentAt DESC
                OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY";

            using var cmd2   = new SqlCommand(sql, conn);
            cmd2.Parameters.AddWithValue("@skip", (CurrentPage - 1) * PageSize);
            cmd2.Parameters.AddWithValue("@take", PageSize);
            using var reader = cmd2.ExecuteReader();
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
