// ============================================================
//  Staff/Chat.cshtml.cs  —  Staff Chat Support Page Model
// ============================================================
//  This page model powers the staff-facing live chat panel at
//  /Staff/Chat.  It is accessible to both STAFF and ADMIN roles.
//
//  Responsibilities:
//    1. OnGet()                         – Load all customer threads +
//                                         the selected thread's messages
//    2. OnPostReplyWithAttachmentAsync() – AJAX endpoint for staff to
//                                         reply with text and/or a file
//    3. OnGetPoll()                     – AJAX endpoint polled every 3 s
//                                         to pick up new customer messages
//    4. OnGetUnreadCount()              – AJAX endpoint polled every 10 s
//                                         by the sidebar badge
//    5. LoadThreads()                   – Private helper that builds the
//                                         left-panel conversation list
//    6. LoadMessages()                  – Private helper that loads the
//                                         right-panel message history
//    7. MarkThreadRead()                – Private helper that marks all
//                                         customer messages in a thread
//                                         as read when a staff opens it
// ============================================================

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace HotelBookingSystem.Pages.Staff
{
    // ── Thread list item ─────────────────────────────────────────────────────
    // Represents one row in the left-panel conversation list.
    // Each thread corresponds to ONE customer who has sent at least one message.
    public class ChatThread
    {
        public int    CustomerId   { get; set; } // userId of the customer
        public string CustomerName { get; set; } = ""; // fullName from Users table
        public string LastMessage  { get; set; } = ""; // Preview text of the most recent message
        public string LastSentAt   { get; set; } = ""; // Human-readable timestamp of latest message
        public int    UnreadCount  { get; set; } // Number of unread messages from this customer
        public int    LastChatId   { get; set; } // chatId of the most recent message (not currently displayed)
    }

    // ── Single message DTO ───────────────────────────────────────────────────
    // Represents one message in the active thread's right-panel history.
    public class StaffChatMessage
    {
        public int    ChatId       { get; set; } // Primary key – used as the "after" cursor for polling
        public int    SenderId     { get; set; } // userId of the sender
        public string SenderName   { get; set; } = ""; // fullName of the sender
        public string Text         { get; set; } = ""; // Plain-text body
        public string AttachmentUrl { get; set; } = ""; // Relative URL to uploaded file, or ""
        public string SentAt       { get; set; } = ""; // Human-readable time, e.g. "10:30 AM"
        public bool   IsFromStaff  { get; set; } // true → gold "sent" bubble; false → grey "received"
    }

    // ── Page model ───────────────────────────────────────────────────────────
    public class StaffChatModel : PageModel
    {
        // ADO.NET connection string from appsettings.json
        private readonly string _conn;

        // IWebHostEnvironment gives access to WebRootPath (wwwroot) for file uploads
        private readonly IWebHostEnvironment _environment;

        // Constructor: uses IConfiguration so both services are resolved by DI.
        // StaffChatModel does NOT take the raw connection-string singleton
        // because it also needs IWebHostEnvironment — so it reads the string
        // from IConfiguration directly.
        public StaffChatModel(IConfiguration configuration, IWebHostEnvironment environment)
        {
            _environment = environment;
            _conn = configuration.GetConnectionString("DefaultConnection")!;
        }

        // ── Bindable page properties ─────────────────────────────────────────

        // All customer threads shown in the left panel (always populated on OnGet)
        public List<ChatThread>       Threads        { get; set; } = new();

        // Messages for the currently selected thread (only populated if a
        // customerId query-string parameter is present)
        public List<StaffChatMessage> ActiveMessages { get; set; } = new();

        // The thread object for the currently selected customer (null if none selected)
        public ChatThread?            ActiveThread   { get; set; }

        // Total unread count across all threads (shown in the page sub-header)
        public int                    UnreadTotal    { get; set; }

        // ── Private helpers ──────────────────────────────────────────────────

        /// <summary>
        /// Reads UserId from the session and returns it as an int.
        /// Returns 0 if the session value is absent.
        /// </summary>
        private int GetStaffId()
        {
            var userId = HttpContext.Session.GetString("UserId");
            return int.TryParse(userId, out var id) ? id : 0;
        }

        /// <summary>
        /// Returns true if the current session belongs to a STAFF or ADMIN user.
        /// Used to guard every handler in this page model.
        /// </summary>
        private bool IsStaff()
        {
            var role = HttpContext.Session.GetString("UserRole");
            return role == "STAFF" || role == "ADMIN";
        }

        // ── Handler: GET /Staff/Chat[?customerId=x] ──────────────────────────
        /// <summary>
        /// Main page load handler.
        ///
        /// Flow:
        ///   1. Redirect to login if the session is not STAFF or ADMIN
        ///   2. Load all customer threads into the left panel
        ///   3. If a customerId is provided in the query string:
        ///        a. Find that thread in the list
        ///        b. Load its messages into the right panel
        ///        c. Mark all of its unread messages as read
        ///   4. Return the rendered page
        ///
        /// The thread-first-then-message approach means the left panel is always
        /// populated, even if the customer has no active thread selected.
        /// </summary>
        public IActionResult OnGet(int? customerId)
        {
            // Step 1 – enforce staff/admin access
            if (!IsStaff()) return Redirect("/Login?returnUrl=/Staff/Chat");

            // Step 2 – populate the thread list (left panel)
            LoadThreads();

            // Step 3 – if a specific conversation is selected, load it
            if (customerId.HasValue && customerId.Value > 0)
            {
                // Find the matching thread object (already loaded in step 2)
                ActiveThread = Threads.FirstOrDefault(t => t.CustomerId == customerId.Value);
                if (ActiveThread != null)
                {
                    // Load the message history for the right-panel conversation view
                    LoadMessages(customerId.Value);

                    // Mark all of this customer's unread messages as read so
                    // the unread badge clears after the staff opens the thread
                    MarkThreadRead(customerId.Value);
                }
            }

            return Page();
        }

        // ── Handler: POST /Staff/Chat?handler=ReplyWithAttachment ────────────
        /// <summary>
        /// AJAX endpoint called by the staff reply form (JavaScript FormData).
        /// Accepts multipart/form-data so that a file can be sent alongside text.
        ///
        /// Bound parameters:
        ///   text        – optional plain-text reply body (from [FromForm])
        ///   customerId  – the customer this reply is addressed to (from [FromForm])
        ///   attachment  – optional file upload (IFormFile from [FromForm])
        ///
        /// File upload flow (same as customer side):
        ///   1. Validate size ≤ 10 MB
        ///   2. Validate extension against an allow-list (images + common docs)
        ///   3. Save to wwwroot/uploads/chat/ with a GUID-prefixed filename
        ///   4. Store the relative URL in the database row
        ///
        /// Database insert:
        ///   senderId   = logged-in staff member
        ///   receiverId = customerId  (unlike customer messages, staff replies
        ///                             are addressed to the specific customer so
        ///                             the customer's HasUnread check works correctly)
        ///   isRead     = 1  (staff replies start as "read" – staff don't need
        ///                    to mark their own replies as unread)
        ///
        /// Returns JSON { ok: true } or { ok: false, error: "..." }
        /// </summary>
        public async Task<IActionResult> OnPostReplyWithAttachmentAsync(
            [FromForm] string text,
            [FromForm] int customerId,
            IFormFile attachment)
        {
            // Guard: only STAFF/ADMIN can reply
            if (!IsStaff()) return new JsonResult(new { ok = false });

            // Guard: reject if neither text nor a file was provided
            if (string.IsNullOrEmpty(text) && attachment == null)
                return new JsonResult(new { ok = false, error = "No content to send" });

            // Track the uploaded file's URL (null if no file was attached)
            string attachmentUrl = null;

            if (attachment != null && attachment.Length > 0)
            {
                // Size limit: 10 MB
                if (attachment.Length > 10 * 1024 * 1024)
                    return new JsonResult(new { ok = false, error = "File too large (max 10MB)" });

                // Allow-list of accepted file types (images + common office documents + videos)
                var allowedExtensions = new[]
                {
                    ".jpg", ".jpeg", ".png", ".gif", ".webp",           // images
                    ".pdf", ".doc", ".docx", ".txt", ".xlsx", ".pptx",  // documents
                    ".mp4", ".webm", ".ogg", ".mov"                     // videos
                };
                var extension = Path.GetExtension(attachment.FileName).ToLower();

                if (!allowedExtensions.Contains(extension))
                    return new JsonResult(new { ok = false, error = "File type not allowed" });

                // Create the uploads directory if it doesn't exist yet
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "chat");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                // Build a unique filename to prevent overwriting existing files
                var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileNameWithoutExtension(attachment.FileName)}{extension}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                // Stream upload to disk asynchronously
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await attachment.CopyToAsync(stream);
                }

                // Relative URL stored in the database (also served by the web server)
                attachmentUrl = $"/uploads/chat/{uniqueFileName}";
            }

            // ── Database insert ──────────────────────────────────────────────
            int staffId = GetStaffId();
            using var conn = new SqlConnection(_conn);
            conn.Open();
            using var cmd = new SqlCommand(
                @"INSERT INTO ChatMessages (senderId, receiverId, messageText, attachmentUrl, isRead, sentAt)
                  VALUES (@s, @r, @t, @a, 1, GETDATE())",
                conn);
            cmd.Parameters.AddWithValue("@s", staffId);                                    // Staff sender
            cmd.Parameters.AddWithValue("@r", customerId);                                 // Customer recipient
            cmd.Parameters.AddWithValue("@t", text ?? "");                                 // Text body
            cmd.Parameters.AddWithValue("@a", attachmentUrl ?? (object)DBNull.Value);      // File URL or NULL
            cmd.ExecuteNonQuery();

            return new JsonResult(new { ok = true });
        }

        // ── Handler: GET /Staff/Chat?handler=Poll&customerId=x&after=y ───────
        /// <summary>
        /// Real-time polling endpoint called by JavaScript every 3 seconds
        /// while the staff member has a conversation open.
        ///
        /// Parameters:
        ///   customerId – the thread currently displayed in the right panel
        ///   after      – chatId of the last message the browser has locally
        ///                (only rows newer than this are returned)
        ///
        /// Query scope:
        ///   - Messages SENT by the customer with no specific receiver (broadcast)
        ///   - Messages SENT by ANY staff member to this customer
        ///   - Messages SENT by this customer to any staff member
        ///   All filtered to chatId > @after for efficiency.
        ///
        /// Side-effect:
        ///   Also marks new customer messages as read (isRead = 1) so the
        ///   unread badge count in the sidebar drops in real time.
        ///
        /// Returns a JSON array of message objects.
        /// </summary>
        public IActionResult OnGetPoll(int customerId, int after = 0)
        {
            if (!IsStaff()) return new JsonResult(new List<object>());

            var msgs = new List<object>();

            using var conn = new SqlConnection(_conn);
            conn.Open();

            // Side-effect: mark this customer's messages as read when polled,
            // so the sidebar badge updates without a full page refresh
            using (var markCmd = new SqlCommand(
                "UPDATE ChatMessages SET isRead = 1 WHERE senderId = @cid AND receiverId IS NULL AND isRead = 0", conn))
            {
                markCmd.Parameters.AddWithValue("@cid", customerId);
                markCmd.ExecuteNonQuery();
            }

            // Fetch all new messages in this thread since the browser's last known ID
            string sql = @"
                SELECT c.chatId, c.senderId, c.messageText, c.attachmentUrl, c.sentAt,
                       u.fullName, u.role
                FROM ChatMessages c
                INNER JOIN Users u ON c.senderId = u.userId
                WHERE (c.senderId = @cid OR c.receiverId = @cid)
                  AND c.chatId > @after
                ORDER BY c.chatId ASC";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@cid",   customerId);
            cmd.Parameters.AddWithValue("@after", after);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                int senderId = (int)reader["senderId"];
                string senderRole = reader["role"].ToString()!;

                // isFromStaff determines bubble direction:
                //   true  → staff spoke → gold bubble on the right ("sent")
                //   false → customer spoke → grey bubble on the left ("received")
                bool isFromStaff = senderRole == "STAFF" || senderRole == "ADMIN";

                msgs.Add(new
                {
                    chatId        = (int)reader["chatId"],
                    senderId      = senderId,
                    senderName    = reader["fullName"].ToString(),
                    text          = reader["messageText"].ToString(),
                    // Map SQL NULL to null (JSON null) so JS can safely check msg.attachmentUrl
                    attachmentUrl = reader["attachmentUrl"] != DBNull.Value
                                    ? reader["attachmentUrl"].ToString()
                                    : null,
                    sentAt        = ((DateTime)reader["sentAt"]).ToString("h:mm tt"),
                    isFromStaff   = isFromStaff
                });
            }

            return new JsonResult(msgs);
        }

        // ── Handler: GET /Staff/Chat?handler=UnreadCount ─────────────────────
        /// <summary>
        /// Lightweight endpoint polled every 10 seconds by the sidebar script
        /// (in _StaffLayout.cshtml) to keep the red unread badge up-to-date
        /// on ALL staff pages, not just the Chat page itself.
        ///
        /// Counts all customer messages that:
        ///   - Have receiverId IS NULL (broadcast, not addressed to a specific staff)
        ///   - Have isRead = 0 (not yet read by any staff)
        ///
        /// Returns JSON { count: N }.
        /// The sidebar JavaScript shows/hides the badge accordingly.
        /// </summary>
        public IActionResult OnGetUnreadCount()
        {
            if (!IsStaff()) return new JsonResult(new { count = 0 });

            using var conn = new SqlConnection(_conn);
            conn.Open();

            // Count unread broadcast messages (from customers to staff in general)
            using var cmd = new SqlCommand(
                "SELECT COUNT(*) FROM ChatMessages WHERE receiverId IS NULL AND isRead = 0", conn);
            int count = (int)cmd.ExecuteScalar();
            return new JsonResult(new { count });
        }

        // ── Private helper: LoadThreads ──────────────────────────────────────
        /// <summary>
        /// Builds the left-panel conversation list by querying the database for
        /// every customer who has at least one message in ChatMessages.
        ///
        /// For each qualifying customer the query uses correlated sub-queries to
        /// retrieve four pieces of metadata in a single pass:
        ///   LastMessage  – text of the most recent message (used as thread preview)
        ///   LastSentAt   – timestamp of the most recent message (for sorting)
        ///   UnreadCount  – count of unread broadcast messages from this customer
        ///   LastChatId   – chatId of the most recent message
        ///
        /// Results are ordered by LastSentAt DESC so the most recently active
        /// conversation always appears at the top of the list.
        ///
        /// Also computes UnreadTotal (sum of all thread UnreadCounts) so the
        /// page header can show "X unread" at a glance.
        /// </summary>
        private void LoadThreads()
        {
            Threads.Clear(); // Ensure a clean list on every page load

            using var conn = new SqlConnection(_conn);
            conn.Open();

            // One query fetches the thread list with all metadata via sub-queries.
            // COALESCE handles the edge case where a customer exists in Users but
            // their messages have been deleted.
            string sql = @"
                SELECT DISTINCT
                    u.userId   AS CustomerId,
                    u.fullName AS CustomerName,
                    COALESCE((
                        SELECT TOP 1 m.messageText
                        FROM ChatMessages m
                        WHERE m.senderId = u.userId OR m.receiverId = u.userId
                        ORDER BY m.chatId DESC
                    ), 'No messages') AS LastMessage,
                    COALESCE((
                        SELECT TOP 1 m.sentAt
                        FROM ChatMessages m
                        WHERE m.senderId = u.userId OR m.receiverId = u.userId
                        ORDER BY m.chatId DESC
                    ), GETDATE()) AS LastSentAt,
                    COALESCE((
                        SELECT COUNT(*)
                        FROM ChatMessages m
                        WHERE m.senderId = u.userId
                          AND m.isRead = 0
                    ), 0) AS UnreadCount,
                    COALESCE((
                        SELECT TOP 1 m.chatId
                        FROM ChatMessages m
                        WHERE m.senderId = u.userId OR m.receiverId = u.userId
                        ORDER BY m.chatId DESC
                    ), 0) AS LastChatId
                FROM Users u
                WHERE u.role = 'CUSTOMER'
                  AND EXISTS (
                      SELECT 1 FROM ChatMessages m
                      WHERE m.senderId = u.userId OR m.receiverId = u.userId
                  )
                ORDER BY LastSentAt DESC";

            using var cmd = new SqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var thread = new ChatThread
                {
                    CustomerId   = (int)reader["CustomerId"],
                    CustomerName = reader["CustomerName"].ToString()!,
                    LastMessage  = reader["LastMessage"].ToString()!,
                    // Format the timestamp for display in the thread list
                    LastSentAt   = reader["LastSentAt"] != DBNull.Value
                                    ? ((DateTime)reader["LastSentAt"]).ToString("MMM d, h:mm tt")
                                    : "",
                    UnreadCount  = (int)reader["UnreadCount"],
                    LastChatId   = (int)reader["LastChatId"]
                };
                Threads.Add(thread);
            }

            // Sum all per-thread unread counts for the page-level summary badge
            UnreadTotal = Threads.Sum(t => t.UnreadCount);
        }

        // ── Private helper: LoadMessages ─────────────────────────────────────
        /// <summary>
        /// Loads the full message history for the selected customer thread and
        /// populates ActiveMessages for Razor to render the right panel.
        ///
        /// Message scope (same as OnGetPoll):
        ///   - Messages sent BY the customer (broadcast — receiverId IS NULL)
        ///   - Messages sent TO the customer by any staff member
        ///   - All ordered by chatId ASC (chronological)
        ///
        /// The sender's role is joined from Users to determine IsFromStaff,
        /// which controls the bubble direction (right = staff / left = customer).
        /// </summary>
        private void LoadMessages(int customerId)
        {
            ActiveMessages.Clear(); // Start fresh every time a thread is opened

            using var conn = new SqlConnection(_conn);
            conn.Open();

            // Load entire thread history for the selected customer
            string sql = @"
                SELECT c.chatId, c.senderId, c.messageText, c.attachmentUrl, c.sentAt,
                       u.fullName, u.role
                FROM ChatMessages c
                INNER JOIN Users u ON c.senderId = u.userId
                WHERE c.senderId = @cid OR c.receiverId = @cid
                ORDER BY c.chatId ASC";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@cid", customerId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                int senderId = (int)reader["senderId"];
                string senderRole = reader["role"].ToString()!;

                // Staff role check: STAFF or ADMIN → bubble on the right ("sent")
                bool isFromStaff = senderRole == "STAFF" || senderRole == "ADMIN";

                ActiveMessages.Add(new StaffChatMessage
                {
                    ChatId        = (int)reader["chatId"],
                    SenderId      = senderId,
                    SenderName    = reader["fullName"].ToString()!,
                    Text          = reader["messageText"].ToString()!,
                    // Map SQL NULL → empty string consistent with ChatMessage model
                    AttachmentUrl = reader["attachmentUrl"] != DBNull.Value
                                    ? reader["attachmentUrl"].ToString()!
                                    : "",
                    SentAt        = ((DateTime)reader["sentAt"]).ToString("h:mm tt"),
                    IsFromStaff   = isFromStaff
                });
            }
        }

        // ── Private helper: MarkThreadRead ───────────────────────────────────
        /// <summary>
        /// Sets isRead = 1 for all unread messages originating from the
        /// specified customer (senderId = customerId, receiverId IS NULL).
        ///
        /// This is called once when a staff member opens a thread (OnGet),
        /// and incrementally when the staff poll runs (OnGetPoll).
        ///
        /// Effect: the unread counter for this thread drops to zero, and
        /// future calls to OnGetUnreadCount will no longer include these messages.
        /// </summary>
        private void MarkThreadRead(int customerId)
        {
            using var conn = new SqlConnection(_conn);
            conn.Open();

            using var cmd = new SqlCommand(
                @"UPDATE ChatMessages
                  SET isRead = 1
                  WHERE senderId = @cid
                    AND isRead = 0",
                conn);
            cmd.Parameters.AddWithValue("@cid", customerId);
            cmd.ExecuteNonQuery();
        }
    }
}