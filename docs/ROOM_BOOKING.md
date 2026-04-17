# Room Booking — How It Works

## Overview

The booking page (`/BookRoom/{roomId}`) allows logged-in customers to reserve a room. It supports two booking types — **overnight (nightly)** and **hourly** — and enforces double-booking prevention before inserting a record.

---

## Database Table

```sql
CREATE TABLE Bookings (
    bookingId    INT IDENTITY(1,1) PRIMARY KEY,
    userId       INT           NOT NULL FOREIGN KEY REFERENCES Users(userId),
    roomId       INT           NOT NULL FOREIGN KEY REFERENCES Rooms(roomId),
    checkInDate  DATE          NOT NULL,
    checkOutDate DATE          NOT NULL,
    guestCount   INT           NOT NULL,
    totalPrice   DECIMAL(10,2) NOT NULL,
    status       VARCHAR(15)   NOT NULL DEFAULT 'PENDING'
                               CHECK (status IN ('PENDING','CONFIRMED','CHECKED_IN','CHECKED_OUT','CANCELLED')),
    createdAt    DATETIME      NOT NULL DEFAULT GETDATE(),
    CONSTRAINT CHK_Dates CHECK (checkOutDate > checkInDate)
);
```

The `CHK_Dates` constraint enforces at the DB level that checkout is always after check-in.

---

## Access Control

```csharp
if (string.IsNullOrEmpty(HttpContext.Session.GetString("UserId")))
    return Redirect($"/Login?returnUrl=/BookRoom/{roomId}");
```

Unauthenticated users are redirected to login with the return URL preserved so they land back on the booking page after signing in.

---

## Room Loading (`LoadRoom`)

```csharp
SELECT roomId, roomNumber, type, pricePerNight, capacity, description
FROM Rooms
WHERE roomId = @id AND isAvailable = 1
```

- Only loads rooms where `isAvailable = 1` — if a room is under maintenance it returns false and the user is redirected to `/Rooms`
- Populates page properties: `RoomId`, `RoomType`, `PricePerNight`, `Capacity`, etc.

---

## Booking Types

### Nightly Booking

```csharp
int nights     = (checkOut - checkIn).Days;
decimal totalPrice = PricePerNight * nights;
```

- User picks check-in and check-out dates
- Price = `pricePerNight × number of nights`
- Validation: `checkOut > checkIn` (also enforced by DB constraint)

### Hourly Booking

```csharp
int hours      = durationHours ?? 1;
checkIn        = hDate.Date;
checkOut       = hDate.Date.AddDays(1);   // satisfies CHK_Dates constraint
totalPrice     = Math.Round(PricePerNight / 8 * hours, 2);
```

- User picks a date, start time, and duration in hours
- Hourly rate = `pricePerNight ÷ 8`
- `checkOutDate` is stored as the next day to satisfy the `CHK_Dates > checkIn` constraint while keeping it a single-day booking

---

## Guest Count Validation

```csharp
if (guestCount < 1 || guestCount > Capacity)
{
    ErrorMessage = $"Guest count must be between 1 and {Capacity}.";
    return Page();
}
```

Capacity comes from the room record — e.g. SINGLE rooms have capacity 1, SUITE rooms have capacity 3.

---

## Double-Booking Prevention

This is the most critical check. It uses a date overlap algorithm:

```sql
SELECT COUNT(*) FROM Bookings
WHERE roomId = @rid
  AND status NOT IN ('CANCELLED','CHECKED_OUT')
  AND checkInDate  < @cout
  AND checkOutDate > @cin
```

### Why this overlap formula works

Two date ranges `[A, B]` and `[C, D]` overlap if and only if `A < D AND B > C`.

Applied here:
- Existing booking's `checkInDate < new checkOutDate` AND
- Existing booking's `checkOutDate > new checkInDate`

If `COUNT(*) > 0`, the room is already booked for those dates and the booking is rejected.

Only `CANCELLED` and `CHECKED_OUT` bookings are excluded — `CONFIRMED` and `CHECKED_IN` both block the dates.

---

## Booking Insert

```sql
INSERT INTO Bookings
    (userId, roomId, checkInDate, checkOutDate, guestCount, totalPrice, status)
VALUES
    (@uid, @rid, @cin, @cout, @guests, @price, 'CONFIRMED')
```

- Status is set to `'CONFIRMED'` immediately on booking (no pending approval step)
- On success, user is redirected to `/BookingConfirmed`

---

## Full Flow

```
Customer visits /BookRoom/{roomId}
        │
        ├── Not logged in? → /Login?returnUrl=...
        ├── Room not found or unavailable? → /Rooms
        │
        ▼
Customer fills form (booking type, dates, guest count)
        │
        ▼
Validate guest count (1 ≤ guests ≤ capacity)
        │
        ▼
Calculate checkIn, checkOut, totalPrice
        │
        ▼
Double-booking check (overlap query)
        │
   Overlap found? → ErrorMessage: "room already booked"
        │
        ▼
INSERT INTO Bookings (..., status = 'CONFIRMED')
        │
        ▼
RedirectToPage("/BookingConfirmed")
```
