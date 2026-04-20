// ============================================================
//  Customer/Chat.cshtml.cs  —  Customer Chat Support Page Model
// ============================================================
//  This page model powers the customer-facing live chat page at
//  /Customer/Chat.  It handles five responsibilities:
//
//    1. OnGet()            – Load the full message history on page load
//    2. OnPostSendAsync()  – AJAX endpoint to send a text message
//                           and/or a file attachment (multipart form)
//    3. OnGetHasUnread()   – AJAX endpoint polled by the floating chat
//                           bubble to decide whether to show a red dot
//    4. OnGetPoll()        – AJAX endpoint polled every 3 s to fetch
//                           any new messages since the last known ID
//    5. LoadMessages()     – Private helper used by OnGet to pre-render
//                           the initial conversation history
// ============================================================

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace HotelBookingSystem.Pages.Customer
{
    // ── Data transfer object ─────────────────────────────────────────────────
    // Represents a single chat message returned to the Razor view.
    // Both the initial server-side render (OnGet) and the AJAX poll
    // (OnGetPoll) map database rows into this shape.
    public class ChatMessage
    {
        public int    ChatId        { get; set; } // Primary key – used as the "after" cursor for polling
        public int    SenderId      { get; set; } // userId of whoever sent the message
        public string SenderName    { get; set; } = ""; // fullName from Users table
        public string Text          { get; set; } = ""; // The plain-text body of the message
        public string AttachmentUrl { get; set; } = ""; // Relative URL to the uploaded file, or "" if none
        public string SentAt        { get; set; } = ""; // Human-readable time string, e.g. "3:45 PM"
        public bool   IsFromMe      { get; set; } // true → bubble aligns right (sent); false → left (received)
    }

    // ── Page model ───────────────────────────────────────────────────────────
    public class ChatModel : PageModel
    {
        // ADO.NET connection string injected from appsettings.json
        private readonly string              _conn;

        // IWebHostEnvironment gives us WebRootPath (wwwroot) so we can
        // build the physical path for saving uploaded files
        private readonly IWebHostEnvironment _env;

        // Constructor – both IConfiguration and IWebHostEnvironment are
        // standard ASP.NET Core services resolved automatically by DI
        public ChatModel(IConfiguration configuration, IWebHostEnvironment env)
        {
            _conn = configuration.GetConnectionString("DefaultConnection")!;
            _env  = env;
        }

        // ── Bindable page properties ─────────────────────────────────────────
        // Full conversation history rendered server-side on first load
        public List<ChatMessage> Messages      { get; set; } = new();

        // Name of the currently logged-in customer (displayed in page header)
        public string            CustomerName  { get; set; } = "";

        // ID of the currently logged-in customer (embedded in the page so
        // JavaScript can determine which bubbles belong to "me")
        public int               CurrentUserId { get; set; }

        // ── Private helpers ──────────────────────────────────────────────────

        /// <summary>
        /// Reads the UserId stored in the session (set on login) and parses it
        /// to an int. Returns 0 if the session value is missing or invalid.
        /// </summary>
        private int GetUserId() =>
            int.TryParse(HttpContext.Session.GetString("UserId"), out var id) ? id : 0;

        /// <summary>
        /// Guard method: if the current session does not belong to a CUSTOMER,
        /// returns a Redirect result to the login page (with a returnUrl so the
        /// user comes back after logging in). Returns null if access is allowed.
        /// </summary>
        private IActionResult? RequireCustomer()
        {
            var role = HttpContext.Session.GetString("UserRole");
            if (role != "CUSTOMER") return Redirect("/Login?returnUrl=/Customer/Chat");
            return null;
        }

        // ── Handler: GET /Customer/Chat ──────────────────────────────────────
        /// <summary>
        /// Standard Razor Pages OnGet handler — runs when the customer navigates
        /// to /Customer/Chat.
        ///
        /// Steps:
        ///   1. Authenticate – redirect to login if not a CUSTOMER
        ///   2. Populate CurrentUserId so the view embeds it into JavaScript
        ///   3. Load full conversation history from the database
        ///   4. Return the rendered page
        /// </summary>
        public IActionResult OnGet()
        {
            // Step 1 – enforce CUSTOMER-only access
            var guard = RequireCustomer();
            if (guard != null) return guard;

            // Step 2 – expose user identity to the view
            CurrentUserId = GetUserId();
            CustomerName  = HttpContext.Session.GetString("UserFullName") ?? "";

            // Step 3 – pre-load the entire conversation so the page renders
            //           with history visible before any JavaScript runs
            LoadMessages(CurrentUserId);
            return Page();
        }

        // ── Handler: POST /Customer/Chat?handler=Send ────────────────────────
        /// <summary>
        /// AJAX endpoint called by the JavaScript sendMsg() function.
        /// Accepts multipart/form-data (not JSON) so that a file attachment
        /// can travel alongside the text in the same HTTP request.
        ///
        /// Parameters (bound from the FormData object in the browser):
        ///   text       – optional plain-text message body
        ///   attachment – optional uploaded file (IFormFile)
        ///
        /// File upload flow:
        ///   1. Validate size ≤ 10 MB
        ///   2. Validate extension against an allow-list
        ///   3. Generate a GUID-prefixed unique filename (prevents collisions)
        ///   4. Save to wwwroot/uploads/chat/
        ///   5. Store the relative URL in the database
        ///
        /// Database insert:
        ///   senderId   = logged-in customer
        ///   receiverId = NULL  (broadcast to all staff – any staff member
        ///                       can see messages with receiverId IS NULL)
        ///   isRead     = 0     (unread until a staff member opens the thread)
        ///
        /// Returns JSON { ok: true } on success or { ok: false, error: "..." }
        /// on validation failure so the JS can surface the error.
        /// </summary>
        public async Task<IActionResult> OnPostSendAsync(
            [FromForm] string?    text,
            [FromForm] IFormFile? attachment)
        {
            // Enforce CUSTOMER-only access for this AJAX endpoint too
            var guard = RequireCustomer();
            if (guard != null) return new JsonResult(new { ok = false });

            // Trim whitespace from the message body
            text = text?.Trim();

            // Reject if neither text nor a file was provided
            if (string.IsNullOrEmpty(text) && (attachment == null || attachment.Length == 0))
                return new JsonResult(new { ok = false, error = "Nothing to send" });

            // ── File upload section ──────────────────────────────────────────
            string? attachmentUrl = null;
            if (attachment != null && attachment.Length > 0)
            {
                // Guard: reject files larger than 10 MB to prevent storage abuse
                if (attachment.Length > 10 * 1024 * 1024)
                    return new JsonResult(new { ok = false, error = "File too large (max 10 MB)" });

                // Allow-list of accepted extensions – images and common office docs
                var allowed = new[] { ".jpg",".jpeg",".png",".gif",".webp",
                                      ".pdf",".doc",".docx",".txt",".xlsx",".pptx" };
                var ext = Path.GetExtension(attachment.FileName).ToLowerInvariant();

                // Guard: reject disallowed extensions (e.g. .exe, .bat)
                if (!allowed.Contains(ext))
                    return new JsonResult(new { ok = false, error = "File type not allowed" });

                // Build the physical save path: wwwroot/uploads/chat/
                // Directory.CreateDirectory is safe to call even if the folder exists
                var folder = Path.Combine(_env.WebRootPath, "uploads", "chat");
                Directory.CreateDirectory(folder);

                // Prefix the original filename with a GUID to avoid name collisions
                // when multiple users upload files with the same name
                var uniqueName = $"{Guid.NewGuid()}{ext}";
                var filePath   = Path.Combine(folder, uniqueName);

                // Stream the upload directly to disk (avoids holding it in memory)
                await using (var fs = new FileStream(filePath, FileMode.Create))
                    await attachment.CopyToAsync(fs);

                // Store the relative URL – this is what goes into the database
                // and what the browser uses to request the file
                attachmentUrl = $"/uploads/chat/{uniqueName}";
            }

            // ── Database insert ──────────────────────────────────────────────
            int userId = GetUserId();
            using var conn = new SqlConnection(_conn);
            conn.Open();
            using var cmd = new SqlCommand(
                @"INSERT INTO ChatMessages (senderId, receiverId, messageText, attachmentUrl, isRead)
                  VALUES (@s, NULL, @t, @a, 0)", conn);

            cmd.Parameters.AddWithValue("@s", userId);           // Who sent it
            cmd.Parameters.AddWithValue("@t", text ?? "");       // Text body (empty string if attachment-only)
            cmd.Parameters.AddWithValue("@a", (object?)attachmentUrl ?? DBNull.Value); // File URL or NULL

            cmd.ExecuteNonQuery();

            return new JsonResult(new { ok = true });
        }

        // ── Handler: GET /Customer/Chat?handler=HasUnread ────────────────────
        /// <summary>
        /// Lightweight AJAX endpoint polled by the floating chat bubble every
        /// 8 seconds from any public page (via _Layout.cshtml).
        ///
        /// Logic: count messages where receiverId = this customer AND isRead = 0.
        /// These are replies sent BY staff TO this specific customer that the
        /// customer has not yet opened in the chat window.
        ///
        /// Returns JSON { hasUnread: true/false }.
        /// The JavaScript shows or hides the red pulse dot on the bubble icon.
        /// </summary>
        public IActionResult OnGetHasUnread()
        {
            var guard = RequireCustomer();
            if (guard != null) return new JsonResult(new { hasUnread = false });

            int userId = GetUserId();
            using var conn = new SqlConnection(_conn);
            conn.Open();

            // Count staff replies addressed to this customer that haven't been read
            using var cmd = new SqlCommand(
                "SELECT COUNT(*) FROM ChatMessages WHERE receiverId = @me AND isRead = 0", conn);
            cmd.Parameters.AddWithValue("@me", userId);
            int count = (int)cmd.ExecuteScalar();

            return new JsonResult(new { hasUnread = count > 0 });
        }

        // ── Handler: GET /Customer/Chat?handler=Poll&after={lastId} ──────────
        /// <summary>
        /// Real-time polling endpoint called by JavaScript every 3 seconds
        /// while the customer has the chat page open.
        ///
        /// The "after" parameter is the chatId of the last message the browser
        /// already has. Returning only rows with chatId > after avoids
        /// re-downloading the entire conversation on every poll.
        ///
        /// Query logic:
        ///   - Includes messages SENT by this customer (senderId = me)
        ///   - Includes messages RECEIVED by this customer (receiverId = me)
        ///   - Only rows with chatId > @after (new since last check)
        ///   - Ordered by chatId ASC so bubbles render in chronological order
        ///
        /// The browser appends each new message bubble to the chat area and
        /// updates its local lastId variable to the highest chatId received.
        ///
        /// Returns a JSON array of message objects.
        /// </summary>
        public IActionResult OnGetPoll(int after = 0)
        {
            var guard = RequireCustomer();
            if (guard != null) return new JsonResult(new List<object>());

            int userId = GetUserId();
            var msgs   = new List<object>();

            using var conn = new SqlConnection(_conn);
            conn.Open();

            // Fetch only new messages since the last known chatId ─────────────
            // Joining Users gives us the sender's name and role without a
            // second query; role is used to set isFromStaff on the client.
            const string sql = @"
                SELECT c.chatId, c.senderId, c.messageText, c.attachmentUrl, c.sentAt,
                       u.fullName, u.role
                FROM ChatMessages c
                JOIN Users u ON c.senderId = u.userId
                WHERE (c.senderId = @me OR c.receiverId = @me)
                  AND c.chatId > @after
                ORDER BY c.chatId ASC";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@me",    userId);
            cmd.Parameters.AddWithValue("@after", after); // Only rows newer than what the browser already has

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                int    sid  = (int)reader["senderId"];
                string role = reader["role"].ToString()!;

                msgs.Add(new
                {
                    chatId        = (int)reader["chatId"],
                    senderId      = sid,
                    senderName    = reader["fullName"].ToString(),
                    text          = reader["messageText"].ToString(),
                    // attachmentUrl is nullable in the DB – map NULL → null in JSON
                    attachmentUrl = reader["attachmentUrl"] != DBNull.Value
                                    ? reader["attachmentUrl"].ToString() : null,
                    sentAt        = ((DateTime)reader["sentAt"]).ToString("h:mm tt"),
                    // isFromMe drives bubble alignment (right=sent, left=received)
                    isFromMe      = sid == userId,
                    // isFromStaff lets the view know if the sender is staff/admin
                    isFromStaff   = role == "STAFF" || role == "ADMIN"
                });
            }

            return new JsonResult(msgs);
        }

        // ── Private helper: LoadMessages ─────────────────────────────────────
        /// <summary>
        /// Loads the FULL conversation history for the given customer and stores
        /// it in the Messages property, which is then enumerated by the Razor
        /// view to server-render the initial bubble list.
        ///
        /// This is called only once per page load (from OnGet).  After that,
        /// new messages arrive via the OnGetPoll AJAX endpoint, so there is no
        /// need to reload the whole history on every poll.
        ///
        /// Query scope:
        ///   senderId   = this customer  (messages the customer sent)
        ///   receiverId = this customer  (staff replies addressed to this customer)
        /// </summary>
        private void LoadMessages(int userId)
        {
            using var conn = new SqlConnection(_conn);
            conn.Open();

            // Retrieve the complete history, oldest first, so the bubbles
            // appear in natural reading order (top = oldest, bottom = newest)
            const string sql = @"
                SELECT c.chatId, c.senderId, c.messageText, c.attachmentUrl, c.sentAt,
                       u.fullName, u.role
                FROM ChatMessages c
                JOIN Users u ON c.senderId = u.userId
                WHERE c.senderId = @me OR c.receiverId = @me
                ORDER BY c.chatId ASC";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@me", userId);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                int sid = (int)reader["senderId"];
                Messages.Add(new ChatMessage
                {
                    ChatId        = (int)reader["chatId"],
                    SenderId      = sid,
                    SenderName    = reader["fullName"].ToString()!,
                    Text          = reader["messageText"].ToString()!,
                    // Map SQL NULL → empty string so the view can safely check IsNullOrEmpty
                    AttachmentUrl = reader["attachmentUrl"] != DBNull.Value
                                    ? reader["attachmentUrl"].ToString()! : "",
                    SentAt        = ((DateTime)reader["sentAt"]).ToString("h:mm tt"),
                    // IsFromMe = true  → render as a gold "sent" bubble on the right
                    // IsFromMe = false → render as a grey "received" bubble on the left
                    IsFromMe      = sid == userId
                });
            }
        }
    }
}
