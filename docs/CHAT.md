# Chat Support System — Feature Documentation

> **Module:** Live Chat Support  
> **Route (Customer):** `/Customer/Chat`  
> **Route (Staff/Admin):** `/Staff/Chat`  
> **Database Table:** `ChatMessages`  
> **Attachment Storage:** `wwwroot/uploads/chat/`

---

## 1. Overview

The Chat Support system allows logged-in **customers** to send real-time messages, photos, and documents to hotel staff without leaving the website. **Staff and Admin** users respond from a dedicated dual-pane inbox that shows all active customer threads.

Communication is implemented with **AJAX polling** (no third-party libraries or WebSockets required), making it compatible with the existing ADO.NET + Razor Pages stack.

---

## 2. Architecture

```
┌─────────────────────────┐        ┌──────────────────────────┐
│   Customer Browser      │        │    Staff Browser         │
│  /Customer/Chat         │        │  /Staff/Chat?customerId=x│
│                         │        │                          │
│  [Text input]           │        │  [Thread list | Messages]│
│  [📷] [📎] [Send ▶]    │        │  [Text input]            │
│                         │        │  [📷] [📎] [Send ▶]     │
└────────────┬────────────┘        └────────────┬─────────────┘
             │  POST FormData                    │  POST FormData
             │  GET Poll every 3s                │  GET Poll every 3s
             ▼                                   ▼
    ┌─────────────────────────────────────────────────┐
    │           ASP.NET Core Razor Pages              │
    │                                                 │
    │  Customer/Chat.cshtml.cs                        │
    │    OnGet()              – Load full history     │
    │    OnPostSendAsync()    – Insert message + file │
    │    OnGetPoll()          – Return new messages   │
    │    OnGetHasUnread()     – Bubble dot check      │
    │                                                 │
    │  Staff/Chat.cshtml.cs                           │
    │    OnGet()              – Load threads + thread │
    │    OnPostReplyWithAttachmentAsync() – Reply     │
    │    OnGetPoll()          – Return new messages   │
    │    OnGetUnreadCount()   – Sidebar badge count   │
    └────────────────────┬────────────────────────────┘
                         │  ADO.NET (SqlConnection)
                         ▼
              ┌─────────────────────┐
              │   SQL Server        │
              │   ChatMessages      │
              └─────────────────────┘
```

---

## 3. Database Schema

### `ChatMessages` Table

```sql
CREATE TABLE ChatMessages (
    chatId        INT IDENTITY(1,1) PRIMARY KEY,
    senderId      INT NOT NULL FOREIGN KEY REFERENCES Users(userId),
    receiverId    INT NULL     FOREIGN KEY REFERENCES Users(userId),
    messageText   VARCHAR(MAX) NOT NULL DEFAULT '',
    attachmentUrl NVARCHAR(500) NULL,
    sentAt        DATETIME     NOT NULL DEFAULT GETDATE(),
    isRead        BIT          NOT NULL DEFAULT 0
);
```

| Column | Description |
|--------|-------------|
| `chatId` | Auto-increment primary key; used as the polling cursor (`after` parameter) |
| `senderId` | FK to `Users.userId` — who sent the message |
| `receiverId` | FK to `Users.userId` — **NULL** for customer broadcasts; set to `customerId` for staff replies |
| `messageText` | Plain-text message body (empty string `''` for attachment-only messages) |
| `attachmentUrl` | Relative URL to the uploaded file (e.g. `/uploads/chat/abc123.pdf`), or NULL |
| `sentAt` | UTC timestamp set by SQL Server `DEFAULT GETDATE()` |
| `isRead` | `0` = unread, `1` = read; drives the unread badge and red dot UX |

### Addressing Convention

| Scenario | `senderId` | `receiverId` | Meaning |
|----------|-----------|-------------|---------|
| Customer sends a message | `customerId` | `NULL` | Broadcast — visible to all staff |
| Staff replies to a customer | `staffId` | `customerId` | Addressed — only this customer sees it |

### Migration (for existing databases)

```sql
-- Run once against HotelBookingDB if the table already exists without attachmentUrl
ALTER TABLE ChatMessages ADD attachmentUrl NVARCHAR(500) NULL;
ALTER TABLE ChatMessages ALTER COLUMN messageText VARCHAR(MAX) NOT NULL;
```

---

## 4. Customer Chat (`/Customer/Chat`)

### Access Control
Only users with `UserRole = "CUSTOMER"` in the session can access this page. All five handlers call `RequireCustomer()` which redirects to `/Login?returnUrl=/Customer/Chat` if the check fails.

### Page Load Flow

```
Customer navigates to /Customer/Chat
  └─ OnGet()
       ├─ RequireCustomer() → redirect if not CUSTOMER
       ├─ Set CurrentUserId (used by JavaScript)
       ├─ LoadMessages(userId)
       │     SELECT all rows WHERE senderId=me OR receiverId=me
       │     ORDER BY chatId ASC
       └─ Return rendered page with full history
```

### Sending a Message

The send button and Enter key both call `sendMsg()` in JavaScript. It builds a `FormData` object and POSTs to `OnPostSendAsync`:

```
sendMsg() in browser
  └─ FormData { text: "...", attachment: File|null }
       └─ POST /Customer/Chat?handler=Send
            └─ OnPostSendAsync()
                 ├─ Validate: text or file must be present
                 ├─ If file:
                 │     ├─ Size check ≤ 10 MB
                 │     ├─ Extension check (allow-list)
                 │     ├─ Save to wwwroot/uploads/chat/{GUID}.ext
                 │     └─ Set attachmentUrl = "/uploads/chat/{GUID}.ext"
                 └─ INSERT INTO ChatMessages (senderId=me, receiverId=NULL, ...)
```

### Real-time Polling

Every **3 seconds** the browser calls `poll()`:

```
poll() [every 3s]
  └─ GET /Customer/Chat?handler=Poll&after={lastId}
       └─ OnGetPoll(after)
            └─ SELECT WHERE (senderId=me OR receiverId=me) AND chatId > @after
                 └─ Return JSON array of new messages
```

The browser appends each new message bubble to the chat area and advances `lastId` to the highest `chatId` received.

### Floating Chat Bubble (all pages)

A floating gold headset icon appears on all public pages for logged-in customers. It polls every **8 seconds**:

```
(async) checkUnread() [every 8s]
  └─ GET /Customer/Chat?handler=HasUnread
       └─ OnGetHasUnread()
            └─ SELECT COUNT(*) WHERE receiverId=me AND isRead=0
                 └─ { hasUnread: true/false }
```

If `hasUnread = true`, a red pulse dot appears on the bubble icon to prompt the customer to open the chat.

---

## 5. Staff Chat (`/Staff/Chat`)

### Access Control
Accessible to `UserRole = "STAFF"` or `UserRole = "ADMIN"`. `IsStaff()` guards every handler and redirects to `/Login?returnUrl=/Staff/Chat` on failure.

### Page Layout

The page is split into two panels:

| Panel | Content |
|-------|---------|
| **Left (300 px)** | Scrollable list of all customers who have sent at least one message |
| **Right (flex)** | The selected conversation with message history and reply input |

Clicking a thread item navigates to `/Staff/Chat?customerId={id}`, which reloads the page with that thread active.

### Thread List (Left Panel)

`LoadThreads()` runs a single SQL query using correlated sub-queries to produce the thread list in one database round-trip:

```sql
SELECT
    u.userId, u.fullName,
    (SELECT TOP 1 messageText ... ORDER BY chatId DESC) AS LastMessage,
    (SELECT TOP 1 sentAt      ... ORDER BY chatId DESC) AS LastSentAt,
    (SELECT COUNT(*) WHERE senderId=u.userId AND isRead=0)  AS UnreadCount,
    (SELECT TOP 1 chatId      ... ORDER BY chatId DESC) AS LastChatId
FROM Users u
WHERE u.role = 'CUSTOMER'
  AND EXISTS (SELECT 1 FROM ChatMessages WHERE senderId=u.userId OR receiverId=u.userId)
ORDER BY LastSentAt DESC
```

Threads with unread messages show a gold dot indicator.

### Replying to a Customer

```
sendMessage() in browser
  └─ FormData { text: "...", customerId: N, attachment: File|null }
       └─ POST /Staff/Chat?handler=ReplyWithAttachment
            └─ OnPostReplyWithAttachmentAsync()
                 ├─ Validate: text or file must be present
                 ├─ If file: size check + extension check + save to wwwroot/uploads/chat/
                 └─ INSERT INTO ChatMessages (senderId=staffId, receiverId=customerId, isRead=1)
```

Staff replies set `receiverId = customerId` so the customer's `HasUnread` query can find them, and `isRead = 1` (staff don't need to be notified of their own replies).

### Real-time Polling (Right Panel)

Every **3 seconds** while a thread is open:

```
poll() [every 3s]
  └─ GET /Staff/Chat?handler=Poll&customerId={id}&after={lastId}
       └─ OnGetPoll(customerId, after)
            ├─ UPDATE ChatMessages SET isRead=1 WHERE senderId=customerId AND isRead=0
            └─ SELECT WHERE (senderId=cid OR receiverId=cid) AND chatId > @after
```

The `UPDATE` in the poll marks new customer messages as read the moment the staff sees them.

### Sidebar Unread Badge

The staff layout (`_StaffLayout.cshtml`) polls every **10 seconds** from any staff page:

```
pollChatUnread() [every 10s, on every staff page]
  └─ GET /Staff/Chat?handler=UnreadCount
       └─ OnGetUnreadCount()
            └─ SELECT COUNT(*) WHERE receiverId IS NULL AND isRead=0
                 └─ { count: N }
```

If `count > 0` the badge shows the number in red next to "Chat Support" in the sidebar.

---

## 6. File Attachment System

### Supported File Types

| Category | Extensions |
|----------|-----------|
| Images | `.jpg` `.jpeg` `.png` `.gif` `.webp` |
| Documents | `.pdf` `.doc` `.docx` `.txt` `.xlsx` `.pptx` |
| Videos (staff only) | `.mp4` `.webm` `.ogg` `.mov` |

### Upload Flow

1. User clicks 📷 (photo) or 📎 (document) button
2. Browser opens OS file picker filtered to the accepted types
3. `handleFile(file)` validates size client-side (≤ 10 MB) and shows a preview strip
4. On send, `currentFile` is appended to the `FormData` alongside the text
5. Server validates size and extension again (server-side is the true guard)
6. File is saved to `wwwroot/uploads/chat/{GUID}.ext`
7. The relative URL `/uploads/chat/{GUID}.ext` is stored in `ChatMessages.attachmentUrl`

### Serving Attachments

Files in `wwwroot/` are served as static files by ASP.NET Core's `MapStaticAssets()`. No special routing is needed — the browser fetches the URL directly.

### Display in Chat

| File type | Rendered as |
|-----------|------------|
| Image | `<img>` tag, click to open full size in new tab |
| Document / other | Download link with file icon and filename |

---

## 7. CSS Classes Reference (`Chat.css`)

| Class | Used by | Description |
|-------|---------|-------------|
| `.chat-bubble-btn` | `_Layout` | The floating headset button on public pages |
| `.chat-bubble-dot` | `_Layout` | Red pulse dot shown when unread replies exist |
| `.chat-page-wrap` | Customer view | Outer container for the customer chat page |
| `.chat-window` | Customer view | White card containing messages + input |
| `.chat-messages-area` | Both | Scrollable message list |
| `.chat-msg.sent` | Both | Gold bubble, right-aligned (sent by current user) |
| `.chat-msg.recv` | Both | Grey bubble, left-aligned (received from other) |
| `.chat-msg-bubble` | Both | The rounded bubble shape |
| `.chat-msg-meta` | Both | Sender name + timestamp below the bubble |
| `.chat-input-area` | Both | Bottom bar holding the input and buttons |
| `.chat-input-box` | Both | The `<textarea>` element |
| `.chat-send-btn` | Both | Gold circular send button |
| `.chat-staff-wrap` | Staff view | Two-column grid (thread list + reply panel) |
| `.chat-thread-list` | Staff view | Left panel container |
| `.chat-thread-item` | Staff view | One row in the thread list |
| `.thread-unread-dot` | Staff view | Gold dot on threads with unread messages |
| `.chat-reply-panel` | Staff view | Right panel container |
| `.nav-unread-badge` | `_StaffLayout` | Red badge on the sidebar nav item |

---

## 8. Polling Interval Summary

| Poller | Location | Interval | Purpose |
|--------|----------|----------|---------|
| Customer message poll | `Customer/Chat.cshtml` | 3 s | Receive new staff replies |
| Staff message poll | `Staff/Chat.cshtml` | 3 s | Receive new customer messages |
| Floating bubble dot | `_Layout.cshtml` | 8 s | Show/hide unread indicator |
| Sidebar badge | `_StaffLayout.cshtml` | 10 s | Show unread count on all staff pages |

---

## 9. Security Considerations

| Measure | Where |
|---------|-------|
| Role-based access guard on every handler | `RequireCustomer()` / `IsStaff()` |
| Anti-forgery token on all POST requests | `@Html.AntiForgeryToken()` + `RequestVerificationToken` header |
| File extension allow-list (server-side) | `OnPostSendAsync`, `OnPostReplyWithAttachmentAsync` |
| File size limit 10 MB (client + server) | JS `handleFile()` + server-side `attachment.Length` check |
| GUID-prefixed filenames | Prevents path traversal and filename guessing |
| SQL parameters (`@param`) everywhere | Prevents SQL injection |

---

## 10. Related Files

| File | Role |
|------|------|
| `Pages/Customer/Chat.cshtml` | Customer chat Razor view |
| `Pages/Customer/Chat.cshtml.cs` | Customer chat page model |
| `Pages/Staff/Chat.cshtml` | Staff chat Razor view |
| `Pages/Staff/Chat.cshtml.cs` | Staff chat page model |
| `wwwroot/css/Chat.css` | All chat-related styles |
| `Pages/Shared/_Layout.cshtml` | Floating bubble + HasUnread polling |
| `Pages/Shared/_StaffLayout.cshtml` | Sidebar nav item + UnreadCount badge |
| `database.txt` | `ChatMessages` schema + migration SQL |
| `wwwroot/uploads/chat/` | Runtime file upload storage directory |
