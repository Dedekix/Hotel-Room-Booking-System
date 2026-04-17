# Events & Event Booking — How It Works

## Overview

The events system has two sides:
- **Customer side** (`/Events`, `/BookEvent`) — browse events and reserve spots
- **Admin side** (`/Staff/Events`, `/Staff/EventBookings`) — create, edit, delete events and view all reservations

---

## Database Tables

```sql
CREATE TABLE Events (
    eventId     INT IDENTITY(1,1) PRIMARY KEY,
    title       VARCHAR(100)  NOT NULL,
    description VARCHAR(255),
    eventDate   DATETIME      NOT NULL,
    location    VARCHAR(100)  NOT NULL,
    capacity    INT           NOT NULL,
    price       DECIMAL(10,2) NOT NULL,
    createdBy   INT           NOT NULL FOREIGN KEY REFERENCES Users(userId),
    imagePath   NVARCHAR(255) NULL
);

CREATE TABLE EventBookings (
    eventBookingId INT IDENTITY(1,1) PRIMARY KEY,
    eventId        INT         NOT NULL FOREIGN KEY REFERENCES Events(eventId),
    userId         INT         NOT NULL FOREIGN KEY REFERENCES Users(userId),
    bookedAt       DATETIME    NOT NULL DEFAULT GETDATE(),
    status         VARCHAR(10) NOT NULL DEFAULT 'PENDING'
                               CHECK (status IN ('PENDING','CONFIRMED','CANCELLED'))
);
```

---

## Customer — Events Page (`/Events`)

### Loading Events

```sql
SELECT eventId, title, description, eventDate, location, capacity, price, imagePath
FROM Events
ORDER BY eventDate
```

All events are shown regardless of login status. Images fall back to keyword matching on the title if no `imagePath` is stored:

```csharp
private static string GetEventImage(string title) => title.ToLower() switch
{
    var t when t.Contains("spa")      => "Images/event-spa.jpg",
    var t when t.Contains("gala")     => "Images/event-gala.jpg",
    var t when t.Contains("jazz")     => "Images/Live jazz.jpg",
    // ... etc
    _                                 => "Images/Fashion.jpg"
};
```

### Loading User's Reservations

```sql
SELECT eb.eventBookingId, e.title, e.eventDate, e.location, eb.status
FROM EventBookings eb
JOIN Events e ON e.eventId = eb.eventId
WHERE eb.userId = @userId AND eb.status != 'CANCELLED'
ORDER BY e.eventDate
```

Only shown if the user is logged in. Cancelled reservations are excluded.

### Booking an Event (Quick Reserve from Events Page)

```csharp
// 1. Check capacity
SELECT e.capacity,
       (SELECT COUNT(*) FROM EventBookings eb
        WHERE eb.eventId = e.eventId AND eb.status != 'CANCELLED') AS booked
FROM Events e WHERE e.eventId = @eventId

// booked + guests > capacity → ErrorMessage: "Not enough spots available"

// 2. Insert
INSERT INTO EventBookings (eventId, userId, status)
VALUES (@eventId, @userId, 'CONFIRMED')
```

### Cancelling a Reservation

```sql
UPDATE EventBookings
SET status = 'CANCELLED'
WHERE eventBookingId = @id AND userId = @userId
```

The `userId` guard ensures customers can only cancel their own reservations.

---

## Customer — Book Event Page (`/BookEvent/{eventId}`)

A dedicated booking page with name/email confirmation.

### Pre-fill from Session

```sql
SELECT fullName, email FROM Users WHERE userId = @id
```

The form is pre-filled with the logged-in user's name and email.

### Validation Checks (in order)

```csharp
// 1. Email must match the logged-in account
SELECT COUNT(*) FROM Users WHERE userId = @id AND email = @email AND isActive = 1
// 0 → ErrorMessage: "Email does not match your account"

// 2. No duplicate booking
SELECT COUNT(*) FROM EventBookings
WHERE eventId = @eid AND userId = @uid AND status != 'CANCELLED'
// > 0 → ErrorMessage: "You have already reserved a spot"

// 3. Capacity check
if (SpotsLeft <= 0) → ErrorMessage: "This event is fully booked"

// 4. Insert
INSERT INTO EventBookings (eventId, userId, status)
VALUES (@eid, @uid, 'CONFIRMED')
```

`SpotsLeft` is calculated in `LoadEvent`:
```csharp
SpotsLeft = Capacity - (int)reader["booked"];
```

---

## Admin — Event Management (`/Staff/Events`)

Admin-only (`role != "ADMIN"` redirects to login).

### Adding an Event

```csharp
// Optional image upload
if (imageFile != null && imageFile.Length > 0)
{
    var savePath = Path.Combine("wwwroot", "Images", fileName);
    imageFile.CopyTo(new FileStream(savePath, FileMode.Create));
    imagePath = $"Images/{fileName}";
}

INSERT INTO Events (title, description, eventDate, location, capacity, price, createdBy, imagePath)
VALUES (@title, @desc, @date, @loc, @cap, @price, @by, @img)
```

- `createdBy` is set to the logged-in admin's `UserId` from session
- Image is saved to `wwwroot/Images/` and the relative path stored in the DB

### Editing an Event

```sql
-- Without new image
UPDATE Events SET title=@title, description=@desc, eventDate=@date,
                  location=@loc, capacity=@cap, price=@price
WHERE eventId=@id

-- With new image
UPDATE Events SET title=@title, ..., imagePath=@img WHERE eventId=@id
```

If no new image is uploaded, `imagePath` is left unchanged.

### Deleting an Event

```sql
-- Must delete child records first (FK constraint)
DELETE FROM EventBookings WHERE eventId = @id
DELETE FROM Events WHERE eventId = @id
```

Child `EventBookings` are deleted first to satisfy the foreign key constraint.

---

## Admin — Event Bookings (`/Staff/EventBookings`)

Read-only view for STAFF and ADMIN showing all event reservations:

```sql
SELECT eb.eventBookingId, u.fullName, u.email,
       e.title, e.eventDate, e.location, e.price,
       eb.status, eb.bookedAt
FROM EventBookings eb
JOIN Users  u ON eb.userId  = u.userId
JOIN Events e ON eb.eventId = e.eventId
ORDER BY eb.bookedAt DESC
```

---

## Capacity Tracking

Available spots are always calculated dynamically — never stored:

```sql
(SELECT COUNT(*) FROM EventBookings eb
 WHERE eb.eventId = e.eventId AND eb.status != 'CANCELLED') AS bookingCount
```

`AvailableSpots = Capacity - BookingCount` (computed property on `EventItem`)

This means cancellations automatically free up spots for new bookings.
