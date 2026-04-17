# My Bookings — How It Works

## Overview

The My Bookings page (`/MyBookings`) is the customer's personal dashboard. It shows all their room bookings and event reservations in one place, and allows them to cancel bookings that are still in `CONFIRMED` status.

---

## Access Control

```csharp
var userId = HttpContext.Session.GetString("UserId");
if (string.IsNullOrEmpty(userId))
    return Redirect("/Login?returnUrl=/MyBookings");
```

Only logged-in users can access this page. The `userId` from session is used for all queries — customers can only ever see their own bookings.

---

## Data Models

```csharp
public class RoomBookingItem
{
    public int      BookingId   { get; set; }
    public string   RoomNumber  { get; set; }
    public string   RoomType    { get; set; }
    public DateTime CheckIn     { get; set; }
    public DateTime CheckOut    { get; set; }
    public int      GuestCount  { get; set; }
    public decimal  TotalPrice  { get; set; }
    public string   Status      { get; set; }
}

public class EventReservationItem
{
    public int      EventBookingId { get; set; }
    public string   EventTitle     { get; set; }
    public DateTime EventDate      { get; set; }
    public string   Location       { get; set; }
    public decimal  Price          { get; set; }
    public string   Status         { get; set; }
}
```

---

## Room Bookings Query

```sql
SELECT b.bookingId, r.roomNumber, r.type,
       b.checkInDate, b.checkOutDate, b.guestCount, b.totalPrice, b.status
FROM Bookings b
JOIN Rooms r ON b.roomId = r.roomId
WHERE b.userId = @uid
ORDER BY b.bookingId DESC
```

- JOINs `Rooms` to get the room number and type
- Filtered by `userId` from session — customers cannot see other users' bookings
- Ordered newest first (`DESC`)

---

## Event Reservations Query

```sql
SELECT eb.eventBookingId, e.title, e.eventDate, e.location, e.price, eb.status
FROM EventBookings eb
JOIN Events e ON eb.eventId = e.eventId
WHERE eb.userId = @uid
ORDER BY eb.eventBookingId DESC
```

Same pattern — JOINs `Events` for display details, filtered by the logged-in user.

---

## Cancelling a Room Booking (`OnPostCancelRoom`)

```csharp
UPDATE Bookings
SET status = 'CANCELLED'
WHERE bookingId = @id
  AND userId = @uid
  AND status = 'CONFIRMED'
```

Three conditions must all be true:
1. `bookingId` matches the submitted ID
2. `userId` matches the session user — prevents one customer cancelling another's booking
3. `status = 'CONFIRMED'` — only confirmed bookings can be cancelled; `CHECKED_IN` or `CHECKED_OUT` bookings cannot

After the update, the page reloads via `return RedirectToPage()`.

---

## Cancelling an Event Reservation (`OnPostCancelEvent`)

```csharp
UPDATE EventBookings
SET status = 'CANCELLED'
WHERE eventBookingId = @id
  AND userId = @uid
```

Similar pattern but without the status restriction — any non-cancelled event reservation can be cancelled.

---

## Full Flow

```
Customer visits /MyBookings
        │
        ├── Not logged in? → /Login?returnUrl=/MyBookings
        │
        ▼
Load room bookings (JOIN Rooms WHERE userId = session)
Load event reservations (JOIN Events WHERE userId = session)
        │
        ▼
Render two sections:
  - Room Bookings table (with Cancel button if status = CONFIRMED)
  - Event Reservations table (with Cancel button)
        │
        ▼
Customer clicks Cancel
        │
        ▼
POST → OnPostCancelRoom or OnPostCancelEvent
        │
UPDATE status = 'CANCELLED' (with userId guard)
        │
        ▼
RedirectToPage() → page reloads with updated data
```

---

## Booking Status Reference

| Status       | Can Cancel | Shown As         |
|--------------|------------|------------------|
| CONFIRMED    | ✅ Yes      | Upcoming booking |
| CHECKED_IN   | ❌ No       | Currently staying|
| CHECKED_OUT  | ❌ No       | Past stay        |
| CANCELLED    | ❌ No       | Cancelled        |
