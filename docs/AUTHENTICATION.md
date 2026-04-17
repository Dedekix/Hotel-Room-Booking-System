# Authentication — How It Works

## Overview

The authentication system handles user registration, login, and logout. It uses **email-only identity** (no passwords) combined with **server-side sessions** to track who is logged in. Customers additionally go through OTP verification — see `OTP_EXPLAINED.md` for that flow.

---

## Database Table

```sql
CREATE TABLE Users (
    userId      INT IDENTITY(1,1) PRIMARY KEY,
    fullName    VARCHAR(100) NOT NULL,
    email       VARCHAR(100) NOT NULL UNIQUE,
    phone       VARCHAR(20),
    role        VARCHAR(10)  NOT NULL CHECK (role IN ('CUSTOMER', 'STAFF', 'ADMIN')),
    isActive    BIT          NOT NULL DEFAULT 1,
    createdAt   DATETIME     NOT NULL DEFAULT GETDATE(),
    lastLoginAt DATETIME     NULL
);
```

Key constraints:
- `email` is `UNIQUE` — enforced at DB level, also checked in code before insert
- `isActive` — soft delete flag; inactive users cannot log in
- `role` — drives access control across the entire app

---

## Signup (`Signup.cshtml.cs`)

### Flow

```
User fills form (FullName, Email, Role)
        │
        ▼
Validate all fields are non-empty
        │
        ▼
SELECT COUNT(*) FROM Users WHERE email = @Email
        │
   count > 0? → ErrorMessage: "email already exists"
        │
        ▼
INSERT INTO Users (fullName, email, role) VALUES (...)
        │
        ▼
RedirectToPage("/Login")
```

### Key implementation details

```csharp
// Duplicate email check
string checkQuery = "SELECT COUNT(*) FROM Users WHERE email = @Email";
int count = (int)checkCmd.ExecuteScalar();
if (count > 0)
{
    ErrorMessage = "An account with this email already exists.";
    return Page();
}

// Role is stored uppercase
cmd.Parameters.AddWithValue("@Role", Role.ToUpper());
```

- Role defaults to `"Customer"` in the form but is stored as `"CUSTOMER"` via `.ToUpper()`
- No password is stored — the system relies on email ownership + OTP for customers

---

## Login (`Login.cshtml.cs`)

### Flow

```
User enters email
        │
        ▼
SELECT userId, fullName, role FROM Users
WHERE email = @Email AND isActive = 1
        │
   Not found? → ErrorMessage: "No account found"
        │
        ▼
Role = STAFF or ADMIN?
   └── Set session keys → redirect to /Staff/Dashboard

Role = CUSTOMER?
   └── Generate OTP → publish to ActiveMQ → redirect to /VerifyOtp
```

### Session keys set on successful login

```csharp
HttpContext.Session.SetString("UserId",       userId);
HttpContext.Session.SetString("UserFullName", fullName);
HttpContext.Session.SetString("UserEmail",    Email);
HttpContext.Session.SetString("UserRole",     role);
```

These four keys are checked throughout the app to determine:
- Whether the user is logged in (`UserId` != null)
- What they are allowed to access (`UserRole`)
- What name to display in the UI (`UserFullName`)

### lastLoginAt update

```csharp
UPDATE Users SET lastLoginAt = GETDATE() WHERE email = @Email
```

Recorded every time a successful login completes (after OTP for customers, immediately for staff/admin).

---

## Logout (`Logout.cshtml.cs`)

```csharp
public IActionResult OnGet()
{
    HttpContext.Session.Clear();
    return RedirectToPage("/Index");
}
```

- Clears the entire session — all keys including `UserId`, `UserRole`, OTP keys, etc.
- Triggered by navigating to `/Logout`
- Redirects to the home page

---

## Route Protection Pattern

Every protected page checks the session at the top of `OnGet`:

```csharp
// Customer pages
if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserId")))
    return Redirect($"/Login?returnUrl=/BookRoom/{roomId}");

// Staff/Admin pages
var role = HttpContext.Session.GetString("UserRole");
if (role != "STAFF" && role != "ADMIN")
    return Redirect("/Login?returnUrl=/Staff/Dashboard");

// Admin-only pages
if (role != "ADMIN")
    return Redirect("/Login?returnUrl=/Staff/Rooms");
```

The `returnUrl` query parameter is preserved through login so the user lands back on the page they were trying to reach.

---

## Role Access Summary

| Page                  | CUSTOMER | STAFF | ADMIN |
|-----------------------|----------|-------|-------|
| `/Index`              | ✅        | ✅     | ✅     |
| `/Rooms`              | ✅        | ✅     | ✅     |
| `/BookRoom`           | ✅        | ❌     | ❌     |
| `/MyBookings`         | ✅        | ❌     | ❌     |
| `/Events`             | ✅        | ✅     | ✅     |
| `/Staff/Dashboard`    | ❌        | ✅     | ✅     |
| `/Staff/Bookings`     | ❌        | ✅     | ✅     |
| `/Staff/EventBookings`| ❌        | ✅     | ✅     |
| `/Staff/Rooms`        | ❌        | ❌     | ✅     |
| `/Staff/Events`       | ❌        | ❌     | ✅     |
| `/Staff/Reports`      | ❌        | ❌     | ✅     |
| `/Staff/Messages`     | ❌        | ❌     | ✅     |
