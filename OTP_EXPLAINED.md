# OTP Authentication — How It Works

## Overview

The OTP (One-Time Password) system adds a second layer of identity verification for **customers only** during login. Instead of a password, a 6-digit code is generated and emailed to the customer. Staff and admins are trusted internal users and bypass OTP entirely.

The system uses **Apache ActiveMQ** as a message broker to decouple OTP generation from email delivery, following a producer/consumer messaging pattern.

---

## Who Gets an OTP?

| Role     | OTP Required | Login Flow              |
|----------|-------------|--------------------------|
| CUSTOMER | ✅ Yes       | Email → OTP → Dashboard |
| STAFF    | ❌ No        | Email → Dashboard        |
| ADMIN    | ❌ No        | Email → Dashboard        |

---

## Architecture

```
[Login Page]
     │
     │  Customer submits email
     ▼
[Login.cshtml.cs]
     │
     ├── Checks DB: does user exist and isActive = 1?
     │
     ├── Role = STAFF or ADMIN?
     │       └── Set session → redirect to /Staff/Dashboard
     │
     └── Role = CUSTOMER?
             │
             ├── Generate 6-digit OTP
             ├── Store OTP + expiry in Session
             └── Publish message to ActiveMQ queue
                          │
                          ▼
               [otp.email.queue]  ← ActiveMQ broker (port 61616)
                          │
                          ▼
               [OtpEmailConsumer]  ← Background hosted service
                          │
                          └── Send OTP email via Gmail SMTP
                                       │
                                       ▼
                              [Customer's Inbox]
                                       │
                          Customer enters OTP on /VerifyOtp
                                       │
                                       ▼
                              [VerifyOtp.cshtml.cs]
                                       │
                          ├── Expired? → show error
                          ├── Wrong code? → show error
                          └── Correct → set session → redirect to /Index
```

---

## Step-by-Step Flow

### Step 1 — Customer Submits Email (`Login.cshtml.cs`)

```csharp
// DB lookup — confirm user exists and is active
SELECT userId, fullName, role FROM Users WHERE email = @Email AND isActive = 1
```

- If no user is found → error shown, login stops.
- If role is `STAFF` or `ADMIN` → session is set immediately, redirected to `/Staff/Dashboard`.
- If role is `CUSTOMER` → proceeds to OTP generation.

---

### Step 2 — OTP Generation (`Login.cshtml.cs`)

```csharp
string otp = Random.Shared.Next(100000, 999999).ToString();

HttpContext.Session.SetString("OtpEmail",  Email);
HttpContext.Session.SetString("OtpCode",   otp);
HttpContext.Session.SetString("OtpExpiry", DateTime.UtcNow.AddMinutes(5).ToString("o"));
```

- A random **6-digit number** is generated (100000–999999).
- Three values are stored in the server-side session:
  - `OtpEmail` — the email the OTP belongs to
  - `OtpCode` — the actual OTP value
  - `OtpExpiry` — UTC timestamp 5 minutes from now (ISO 8601 format)
- The user is then redirected to `/VerifyOtp`.

---

### Step 3 — Publishing to ActiveMQ (`OtpEmailService.cs`)

```csharp
var factory  = new ConnectionFactory("activemq:tcp://localhost:61616");
var session  = conn.CreateSession(AcknowledgementMode.AutoAcknowledge);
var dest     = session.GetQueue("otp.email.queue");
var producer = session.CreateProducer(dest);

producer.Send(session.CreateTextMessage($"{toEmail}|{otp}"));
```

- A connection is opened to the ActiveMQ broker running on `localhost:61616`.
- A text message in the format `email|otp` (e.g. `customer@gmail.com|482910`) is sent to the queue named `otp.email.queue`.
- `AutoAcknowledge` means the broker marks the message as consumed as soon as it is delivered to a consumer.
- The producer disconnects immediately after sending — it does not wait for the email to be sent.

---

### Step 4 — Consuming the Message & Sending Email (`OtpEmailConsumer.cs`)

`OtpEmailConsumer` is a **BackgroundService** — it starts when the application starts and runs continuously in the background.

```csharp
while (!stoppingToken.IsCancellationRequested)
{
    var msg = consumer.Receive(TimeSpan.FromSeconds(2)) as ITextMessage;
    if (msg != null)
    {
        var parts = msg.Text.Split('|');  // ["customer@gmail.com", "482910"]
        await SendEmailAsync(parts[0], parts[1]);
    }
}
```

- Every 2 seconds it polls the `otp.email.queue` for new messages.
- When a message arrives, it splits the `email|otp` text and calls `SendEmailAsync`.

```csharp
using var smtp = new SmtpClient("smtp.gmail.com", 587)
{
    Credentials = new NetworkCredential("noahloic0123@gmail.com", "<app-password>"),
    EnableSsl   = true
};

var mail = new MailMessage(from, toEmail)
{
    Subject = "Your Grand Haven OTP Code",
    Body    = $"Your one-time password is: {otp}\n\nThis code expires in 5 minutes."
};

await smtp.SendMailAsync(mail);
```

- Connects to Gmail SMTP on port **587 with TLS**.
- Authenticates using a **Gmail App Password** (not the account password).
- Sends the OTP email to the customer.

---

### Step 5 — Customer Enters OTP (`VerifyOtp.cshtml.cs`)

```csharp
var email     = HttpContext.Session.GetString("OtpEmail");
var storedOtp = HttpContext.Session.GetString("OtpCode");
var expiryStr = HttpContext.Session.GetString("OtpExpiry");
```

Three validations run in order:

| Check | Condition | Result |
|-------|-----------|--------|
| Session missing | Any session key is null | Redirect to `/Login` |
| Expired | `DateTime.UtcNow > expiry` | Error: "OTP has expired" |
| Wrong code | Submitted OTP ≠ stored OTP | Error: "Invalid OTP" |

If all checks pass:

```csharp
// Clean up OTP session keys
HttpContext.Session.Remove("OtpCode");
HttpContext.Session.Remove("OtpExpiry");
HttpContext.Session.Remove("OtpEmail");

// Re-query DB and set login session
HttpContext.Session.SetString("UserId",       ...);
HttpContext.Session.SetString("UserFullName", ...);
HttpContext.Session.SetString("UserEmail",    ...);
HttpContext.Session.SetString("UserRole",     ...);

// Update last login timestamp
UPDATE Users SET lastLoginAt = GETDATE() WHERE email = @e
```

- OTP session keys are removed to prevent reuse.
- Full login session is established.
- Customer is redirected to `/Index`.

---

## Configuration (`appsettings.json`)

```json
"Email": {
  "Username": "noahloic0123@gmail.com",
  "Password": "<gmail-app-password>",
  "SmtpHost": "smtp.gmail.com",
  "SmtpPort": 587
},
"ActiveMQ": {
  "BrokerUri": "activemq:tcp://localhost:61616",
  "OtpQueue":  "otp.email.queue"
}
```

---

## Why ActiveMQ?

Without a message broker, the login request would have to wait for the SMTP call to complete before redirecting the user — this adds latency and means a slow/failed email blocks the login entirely.

With ActiveMQ:

- `Login.cshtml.cs` publishes the message and **immediately redirects** the user to `/VerifyOtp` — fast response.
- `OtpEmailConsumer` handles the email **asynchronously in the background**.
- If the SMTP server is temporarily slow, messages queue up and are processed without losing them.

---

## Security Notes

- OTP expires after **5 minutes** — short window reduces brute-force risk.
- OTP session keys are **removed after successful verification** — cannot be reused.
- If `/VerifyOtp` is accessed directly without going through login, the missing `OtpEmail` session key causes an immediate redirect back to `/Login`.
- The Gmail credential used is an **App Password**, not the real account password — it can be revoked independently without affecting the Gmail account.
