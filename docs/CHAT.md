# Live Chat — How It Works

## Overview

The live chat system enables real-time text and file messaging between customers and hotel staff. It consists of two pages:

- `/Customer/Chat` — the customer's chat window
- `/Staff/Chat` — the staff inbox with a thread list and reply panel

Messages are delivered via **JavaScript polling** (every 3 seconds) rather than WebSockets. Both sides share the same `ChatMessages` database table.

---

## Database Table

```sql
CREATE TABLE ChatMessages (
    chatId        INT IDENTITY(1,1) PRIMARY KEY,
    senderId      INT           NOT NULL FOREIGN KEY REFERENCES Users(userId),
    receiverId    INT           NULL     FOREIGN KEY REFERENCES Users(userId),
    messageText   VARCHAR(MAX)  NOT NULL DEFAULT '',
    attachmentUrl NVARCHAR(500) NULL,
    sentAt        DATETIME      NOT NULL DEFAULT GETDATE(),
    isRead        BIT           NOT NULL DEFAULT 0
);
```

Key design decisions:

| Column | Purpose |
|--------|---------|
| `receiverId NULL` | Customer messages are broadcast (NULL = visible to all staff). Staff replies set this to the specific customer's `userId` |
| `attachmentUrl NULL` | Stores the relative URL of an uploaded file, or NULL if the message is text-only |
| `isRead` | Drives unread badges — `0` = unread, `1` = read |
| `messageText DEFAULT ''` | Allows attachment-only messages with an empty string body |

Migration for existing databases:
```sql
ALTER TABLE ChatMessages ADD attachmentUrl NVARCHAR(500) NULL;
ALTER TABLE ChatMessages ALTER COLUMN messageText VARCHAR(MAX) NOT NULL;
```

---

## File Storage

Uploaded files are saved to:

```
wwwroot/uploads/chat/<guid><extension>
```

- The GUID prefix prevents filename collisions when multiple users upload files with the same name
- The relative URL stored in the database (`/uploads/chat/<filename>`) is served directly by ASP.NET Core's static file middleware
- The `wwwroot/uploads/chat/` directory is created automatically on first upload if it does not exist

---

## Customer Chat (`/Customer/Chat`)

### Access Control

```csharp
private IActionResult? RequireCustomer()
{
    var role = HttpContext.Session.GetString("UserRole");
    if (role != "CUSTOMER") return Redirect("/Login?returnUrl=/Customer/Chat");
    return null;
}
```

Only `CUSTOMER` sessions can access this page or its AJAX endpoints.

---

### Page Load — `OnGet()`

1. Calls `RequireCustomer()` — redirects if not a customer
2. Sets `CurrentUserId` and `CustomerName` from session
3. Calls `LoadMessages(userId)` to pre-render the full conversation history server-side

`CurrentUserId` is embedded into the Razor view so JavaScript can determine which bubbles belong to "me":

```razor
int myId = Model.CurrentUserId;
// ...
const myId = @myId;
```

---

### Loading History — `LoadMessages(int userId)`

```sql
SELECT c.chatId, c.senderId, c.messageText, c.attachmentUrl, c.sentAt,
       u.fullName, u.role
FROM ChatMessages c
JOIN Users u ON c.senderId = u.userId
WHERE c.senderId = @me OR c.receiverId = @me
ORDER BY c.chatId ASC
```

Scope:
- Messages **sent by** this customer (`senderId = me`)
- Messages **sent to** this customer by staff (`receiverId = me`)
- Ordered oldest-first so bubbles render in natural reading order

`attachmentUrl` is mapped from SQL `NULL` to an empty string so the Razor view can safely use `string.IsNullOrEmpty()`.

---

### Sending a Message — `OnPostSendAsync()`

Called by the JavaScript `sendMsg()` function via a `multipart/form-data` POST.

**Parameters (from FormData):**
- `text` — optional plain-text body
- `attachment` — optional `IFormFile`

**File upload validation:**
```csharp
// Size limit
if (attachment.Length > 10 * 1024 * 1024)
    return new JsonResult(new { ok = false, error = "File too large (max 10 MB)" });

// Extension allow-list
var allowed = new[] { ".jpg",".jpeg",".png",".gif",".webp",
                      ".pdf",".doc",".docx",".txt",".xlsx",".pptx" };
```

**File save:**
```csharp
var uniqueName = $"{Guid.NewGuid()}{ext}";
var filePath   = Path.Combine(_env.WebRootPath, "uploads", "chat", uniqueName);
await using (var fs = new FileStream(filePath, FileMode.Create))
    await attachment.CopyToAsync(fs);
attachmentUrl = $"/uploads/chat/{uniqueName}";
```

**Database insert:**
```sql
INSERT INTO ChatMessages (senderId, receiverId, messageText, attachmentUrl, isRead)
VALUES (@s, NULL, @t, @a, 0)
```

`receiverId = NULL` means the message is broadcast — any staff member can see it.

Returns `{ ok: true }` on success or `{ ok: false, error: "..." }` on validation failure.

---

### Real-time Polling — `OnGetPoll(int after)`

Called by JavaScript every 3 seconds while the chat page is open.

```sql
SELECT c.chatId, c.senderId, c.messageText, c.attachmentUrl, c.sentAt,
       u.fullName, u.role
FROM ChatMessages c
JOIN Users u ON c.senderId = u.userId
WHERE (c.senderId = @me OR c.receiverId = @me)
  AND c.chatId > @after
ORDER BY c.chatId ASC
```

- `@after` is the `chatId` of the last message the browser already has — only newer rows are returned
- The browser updates its local `lastId` to the highest `chatId` received
- Returns a JSON array; the JS `renderBubble()` function appends each new message to the DOM

---

### Unread Indicator — `OnGetHasUnread()`

Polled every 8 seconds by the floating chat bubble in `_Layout.cshtml`.

```sql
SELECT COUNT(*) FROM ChatMessages WHERE receiverId = @me AND isRead = 0
```

Returns `{ hasUnread: true/false }`. The JavaScript shows or hides the red pulse dot on the bubble icon.

---

## Staff Chat (`/Staff/Chat`)

### Access Control

```csharp
private bool IsStaff()
{
    var role = HttpContext.Session.GetString("UserRole");
    return role == "STAFF" || role == "ADMIN";
}
```

Both `STAFF` and `ADMIN` roles can access the staff chat page.

---

### Two-Panel Layout

The page uses a split layout:

- **Left panel** — scrollable thread list, one row per customer who has sent at least one message
- **Right panel** — full conversation history for the selected thread, with a reply input at the bottom

A thread is selected by navigating to `/Staff/Chat?customerId=X`.

---

### Loading Threads — `LoadThreads()`

Builds the left panel with one query using correlated sub-queries:

```sql
SELECT DISTINCT
    u.userId   AS CustomerId,
    u.fullName AS CustomerName,
    COALESCE((
        SELECT TOP 1 m.messageText FROM ChatMessages m
        WHERE m.senderId = u.userId OR m.receiverId = u.userId
        ORDER BY m.chatId DESC
    ), 'No messages') AS LastMessage,
    COALESCE((
        SELECT TOP 1 m.sentAt FROM ChatMessages m
        WHERE m.senderId = u.userId OR m.receiverId = u.userId
        ORDER BY m.chatId DESC
    ), GETDATE()) AS LastSentAt,
    COALESCE((
        SELECT COUNT(*) FROM ChatMessages m
        WHERE m.senderId = u.userId AND m.isRead = 0
    ), 0) AS UnreadCount,
    COALESCE((
        SELECT TOP 1 m.chatId FROM ChatMessages m
        WHERE m.senderId = u.userId OR m.receiverId = u.userId
        ORDER BY m.chatId DESC
    ), 0) AS LastChatId
FROM Users u
WHERE u.role = 'CUSTOMER'
  AND EXISTS (
      SELECT 1 FROM ChatMessages m
      WHERE m.senderId = u.userId OR m.receiverId = u.userId
  )
ORDER BY LastSentAt DESC
```

- Only customers with at least one message appear in the list
- Ordered by most recent activity so the most active thread is always at the top
- `UnreadTotal` is computed as the sum of all thread `UnreadCount` values for the page header badge

---

### Loading a Thread — `LoadMessages(int customerId)`

```sql
SELECT c.chatId, c.senderId, c.messageText, c.attachmentUrl, c.sentAt,
       u.fullName, u.role
FROM ChatMessages c
INNER JOIN Users u ON c.senderId = u.userId
WHERE c.senderId = @cid OR c.receiverId = @cid
ORDER BY c.chatId ASC
```

The sender's `role` is used to set `IsFromStaff`, which controls bubble direction:
- `IsFromStaff = true` → gold bubble on the right (staff sent it)
- `IsFromStaff = false` → grey bubble on the left (customer sent it)

---

### Marking a Thread as Read — `MarkThreadRead(int customerId)`

Called when a staff member opens a thread:

```sql
UPDATE ChatMessages
SET isRead = 1
WHERE senderId = @cid AND isRead = 0
```

This clears the unread badge for that thread immediately on page load.

---

### Replying — `OnPostReplyWithAttachmentAsync()`

Called by the staff reply form via `multipart/form-data` POST.

**Parameters:**
- `text` — optional plain-text reply
- `customerId` — the customer being replied to
- `attachment` — optional `IFormFile`

File validation is identical to the customer side, with the addition of video formats:

```csharp
var allowedExtensions = new[]
{
    ".jpg", ".jpeg", ".png", ".gif", ".webp",
    ".pdf", ".doc", ".docx", ".txt", ".xlsx", ".pptx",
    ".mp4", ".webm", ".ogg", ".mov"   // videos (staff side only)
};
```

**Database insert:**
```sql
INSERT INTO ChatMessages (senderId, receiverId, messageText, attachmentUrl, isRead, sentAt)
VALUES (@s, @r, @t, @a, 1, GETDATE())
```

- `receiverId = customerId` — the reply is addressed to the specific customer
- `isRead = 1` — staff replies start as read (staff don't need to mark their own messages)

---

### Staff Polling — `OnGetPoll(int customerId, int after)`

Called every 3 seconds while a thread is open.

```sql
SELECT c.chatId, c.senderId, c.messageText, c.attachmentUrl, c.sentAt,
       u.fullName, u.role
FROM ChatMessages c
INNER JOIN Users u ON c.senderId = u.userId
WHERE (c.senderId = @cid OR c.receiverId = @cid)
  AND c.chatId > @after
ORDER BY c.chatId ASC
```

Side-effect: also marks new customer messages as read in real time:

```sql
UPDATE ChatMessages SET isRead = 1
WHERE senderId = @cid AND receiverId IS NULL AND isRead = 0
```

This means the unread badge drops without requiring a full page reload.

---

### Sidebar Unread Badge — `OnGetUnreadCount()`

Polled every 10 seconds by the script in `_StaffLayout.cshtml` so the badge stays current on **all** staff pages, not just the Chat page.

```sql
SELECT COUNT(*) FROM ChatMessages WHERE receiverId IS NULL AND isRead = 0
```

Returns `{ count: N }`. The sidebar JavaScript shows or hides the badge accordingly.

---

## Attachment Rendering

Both the Razor server-render and the JavaScript poll use the same rendering logic:

| File type | Rendered as |
|-----------|-------------|
| `.jpg`, `.jpeg`, `.png`, `.gif`, `.webp` | `<img>` tag, clickable to open full size in a new tab |
| `.mp4`, `.webm`, `.ogg`, `.mov` | `<video controls>` element (staff side only) |
| All other types | Styled download link with file icon and filename |

---

## Floating Chat Bubble

The bubble is rendered in `_Layout.cshtml` and appears on all customer-facing pages. It polls `OnGetHasUnread` every 8 seconds. When `hasUnread: true` is returned, a red pulsing dot is shown on the bubble icon to alert the customer of a new staff reply.

---

## Full Flow

### Customer sends a message

```
Customer types message / selects file → clicks Send (or presses Enter)
        │
        ▼
JavaScript builds FormData { text, attachment }
        │
        ▼
POST /Customer/Chat?handler=Send
        │
        ├── Validate: not empty, file ≤ 10 MB, allowed extension
        ├── Save file to wwwroot/uploads/chat/<guid><ext>
        │
        ▼
INSERT INTO ChatMessages (senderId=customer, receiverId=NULL, ...)
        │
        ▼
poll() called immediately → new bubble appended to chat area
```

### Staff replies

```
Staff selects thread → /Staff/Chat?customerId=X
        │
        ▼
LoadThreads() + LoadMessages(X) + MarkThreadRead(X)
        │
Staff types reply / selects file → clicks Send
        │
        ▼
POST /Staff/Chat?handler=ReplyWithAttachment { text, customerId, attachment }
        │
        ├── Validate file (size + extension)
        ├── Save file to wwwroot/uploads/chat/<guid><ext>
        │
        ▼
INSERT INTO ChatMessages (senderId=staff, receiverId=customer, isRead=1, ...)
        │
        ▼
poll() called immediately → reply bubble appears in staff panel
        │
        ▼
Customer's poll (every 3 s) picks up the reply → bubble appears on customer side
Customer's HasUnread poll (every 8 s) → red dot shown on chat bubble
```
