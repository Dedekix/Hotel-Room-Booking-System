# Payment — How It Works

## Overview

The payment page (`/Customer/Payment`) handles payment for both **room bookings** and **event reservations**. It is reached after a booking is created and supports four payment methods: Credit Card, Debit Card, Mobile Money, and In Person.

Online payments immediately confirm the booking. In-person payments leave the booking as `PENDING` until staff processes it at the front desk.

---

## Database Tables

```sql
CREATE TABLE Payments (
    paymentId      INT IDENTITY(1,1) PRIMARY KEY,
    bookingId      INT           NULL FOREIGN KEY REFERENCES Bookings(bookingId),
    eventBookingId INT           NULL FOREIGN KEY REFERENCES EventBookings(eventBookingId),
    amount         DECIMAL(10,2) NOT NULL,
    paymentDate    DATETIME      NOT NULL DEFAULT GETDATE(),
    method         VARCHAR(50)   NOT NULL,
    status         VARCHAR(50)   NOT NULL CHECK (status IN ('PENDING','COMPLETED','FAILED','REFUNDED'))
);
```

Key design decisions:
- `bookingId` and `eventBookingId` are both **nullable** — exactly one will be set per row depending on what is being paid for
- `bookingId INT NULL` was applied via `ALTER TABLE Payments ALTER COLUMN bookingId INT NULL` to support event payments
- `eventBookingId` was added via `ALTER TABLE Payments ADD eventBookingId INT NULL FOREIGN KEY REFERENCES EventBookings(eventBookingId)`

---

## Access Control

```csharp
if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserId")))
    return Redirect("/Login");
```

Any unauthenticated request is redirected to login. No role check beyond being logged in is needed since only customers reach this page via the booking flow.

---

## Page Entry — `OnGet(string type, int id)`

The page is reached with two query parameters:

| Parameter | Values | Meaning |
|-----------|--------|---------|
| `type` | `ROOM` or `EVENT` | What is being paid for |
| `id` | integer | `bookingId` or `eventBookingId` |

Example URLs:
```
/Customer/Payment?type=ROOM&id=12
/Customer/Payment?type=EVENT&id=5
```

`OnGet` calls `LoadSummary()` to populate the booking details. If the booking is not found (or doesn't belong to the logged-in user), the user is redirected to the home page.

---

## Loading the Booking Summary — `LoadSummary()`

Fetches just enough data to display the summary card and calculate the amount due.

### Room booking

```sql
SELECT r.roomNumber, r.type, b.checkInDate, b.checkOutDate, b.totalPrice
FROM Bookings b
JOIN Rooms r ON b.roomId = r.roomId
WHERE b.bookingId = @id AND b.userId = @uid
```

Produces a summary like: `Room 301 (SUITE) — May 10 to May 12, 2026`

### Event reservation

```sql
SELECT e.title, e.eventDate, e.price
FROM EventBookings eb
JOIN Events e ON eb.eventId = e.eventId
WHERE eb.eventBookingId = @id AND eb.userId = @uid
```

Produces a summary like: `Anniversary Gala — May 1, 2026`

The `AND b.userId = @uid` / `AND eb.userId = @uid` clause prevents customers from paying for someone else's booking by guessing an ID.

---

## Processing Payment — `OnPost(string type, int id, string method)`

Called when the customer submits the payment form.

### Step 1 — Reload summary

`LoadSummary()` is called again to re-fetch `Amount` and `Summary` in case the POST arrives without the values in model state.

### Step 2 — Validate method

```csharp
if (string.IsNullOrWhiteSpace(method))
{
    ErrorMessage = "Please select a payment method.";
    return Page();
}
```

### Step 3 — Insert payment record

**Room booking:**

```sql
INSERT INTO Payments (bookingId, amount, method, status)
VALUES (@bid, @amt, @method, @pstatus)
```

**Event reservation:**

```sql
INSERT INTO Payments (bookingId, eventBookingId, amount, method, status)
VALUES (NULL, @eid, @amt, @method, @pstatus)
```

`@pstatus` is set based on the method:

```csharp
bool isInPerson = method == "In Person";
// isInPerson → "PENDING", otherwise → "COMPLETED"
```

### Step 4 — Update booking status (online payments only)

If the payment is **not** in-person, the booking is immediately confirmed:

**Room:**
```sql
UPDATE Bookings SET status = 'CONFIRMED' WHERE bookingId = @id
```

**Event:**
```sql
UPDATE EventBookings SET status = 'CONFIRMED' WHERE eventBookingId = @id
```

In-person payments skip this step — the booking stays `PENDING` until staff manually processes it.

### Step 5 — Redirect

```csharp
return RedirectToPage("/Customer/BookingConfirmed", new { type, id, paid = !isInPerson });
```

The `paid` flag tells the confirmation page whether to show a "payment confirmed" or "pay at front desk" message.

---

## Payment Methods UI

The form renders four method cards. JavaScript controls which additional fields are shown:

| Method | Extra fields shown |
|--------|--------------------|
| Credit Card | Cardholder name, card number, expiry, CVV |
| Debit Card | Same as Credit Card |
| Mobile Money | Phone number field |
| In Person | Info notice: "Your booking will remain Pending until staff confirms payment" |

The button label also changes:
- Online methods → `Pay $X.XX`
- In Person → `Confirm Booking (Pay Later)`

Card number input uses a `formatCard()` function that inserts spaces every 4 digits as the user types.

---

## Full Flow

```
Customer completes booking → redirected to /Customer/Payment?type=X&id=Y
        │
        ├── Not logged in? → /Login
        ├── Booking not found or wrong user? → /Index
        │
        ▼
Page renders with booking summary and amount due
        │
Customer selects payment method and submits
        │
        ├── No method selected? → ErrorMessage shown, stay on page
        │
        ▼
INSERT INTO Payments (..., status = PENDING or COMPLETED)
        │
        ├── Online payment? → UPDATE Bookings/EventBookings SET status = 'CONFIRMED'
        │
        ▼
RedirectToPage("/Customer/BookingConfirmed", { type, id, paid })
```
