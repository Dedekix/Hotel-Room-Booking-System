# Contact Messages — How It Works

## Overview

The contact system has two parts:
- **Customer side** (`/Contact`) — a public form anyone can submit
- **Admin side** (`/Staff/Messages`) — admin inbox to read and mark messages

---

## Database Table

```sql
CREATE TABLE ContactMessages (
    messageId  INT IDENTITY(1,1) PRIMARY KEY,
    fullName   VARCHAR(100)  NOT NULL,
    email      VARCHAR(100)  NOT NULL,
    subject    VARCHAR(200)  NOT NULL,
    message    VARCHAR(MAX)  NOT NULL,
    sentAt     DATETIME      NOT NULL DEFAULT GETDATE(),
    isRead     BIT           NOT NULL DEFAULT 0
);
```

`isRead` defaults to `0` (unread). The admin marks it `1` after reading.

---

## Customer — Contact Form (`/Contact`)

No login required — the form is publicly accessible.

### Submission (`OnPost`)

```csharp
// Validate all fields
if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email) ||
    string.IsNullOrWhiteSpace(subject)  || string.IsNullOrWhiteSpace(message))
{
    ErrorMessage = "All fields are required.";
    return Page();
}

// Insert
INSERT INTO ContactMessages (fullName, email, subject, message)
VALUES (@fullName, @email, @subject, @message)
```

- `sentAt` and `isRead` are set by DB defaults (`GETDATE()` and `0`)
- On success, `Sent = true` is set and the page re-renders with a confirmation message (no redirect)

---

## Admin — Messages Inbox (`/Staff/Messages`)

Admin-only (`role != "ADMIN"` redirects to login).

### Loading Messages

```sql
SELECT messageId, fullName, email, subject, message, sentAt, isRead
FROM ContactMessages
ORDER BY sentAt DESC
```

All messages, newest first. Unread messages are visually highlighted in the UI via the `IsRead` flag.

### Marking as Read (`OnPost`)

```csharp
UPDATE ContactMessages SET isRead = 1 WHERE messageId = @id
```

Triggered when the admin clicks "Mark as Read" on a message. After the update, `LoadMessages()` is called and the page re-renders with the updated read status.

### Unread Count on Dashboard

The Staff Dashboard shows a pending messages badge:

```sql
SELECT COUNT(*) FROM ContactMessages WHERE isRead = 0
```

This count is loaded every time the dashboard is visited, giving staff a live indicator of unread messages.

---

## Full Flow

```
── Customer Side ─────────────────────────────────
Visitor fills contact form → POST OnPost
        │
        ├── Missing fields? → ErrorMessage
        │
        └── INSERT INTO ContactMessages
                │
                ▼
            Sent = true → confirmation shown on page

── Admin Side ────────────────────────────────────
Admin visits /Staff/Messages
        │
        ├── Not ADMIN? → /Login
        │
        ▼
SELECT all messages ORDER BY sentAt DESC
        │
        ▼
Render inbox (unread messages highlighted)
        │
Admin clicks "Mark as Read" → POST OnPost(messageId)
        │
UPDATE ContactMessages SET isRead = 1 WHERE messageId = @id
        │
        ▼
LoadMessages() → page re-renders with updated status
```
