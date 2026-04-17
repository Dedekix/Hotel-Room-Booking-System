# Admin Room Management — How It Works

## Overview

The Room Management page (`/Staff/Rooms`) is **admin-only**. It allows admins to add new rooms, edit existing ones, and toggle room availability (e.g. put a room into maintenance). Staff cannot access this page.

---

## Access Control

```csharp
var role = HttpContext.Session.GetString("UserRole");
if (role != "ADMIN")
{
    Response.Redirect("/Login?returnUrl=/Staff/Rooms");
    return;
}
```

Only `ADMIN` role is permitted. STAFF users are redirected.

---

## Data Model (`RoomItem`)

```csharp
public class RoomItem
{
    public int     RoomId        { get; set; }
    public string  RoomNumber    { get; set; }
    public string  Type          { get; set; }
    public decimal PricePerNight { get; set; }
    public int     Capacity      { get; set; }
    public bool    IsAvailable   { get; set; }
    public string  Description   { get; set; }
    public string  DisplayStatus { get; set; }  // "Available" | "Occupied" | "Maintenance"
}
```

---

## Loading Rooms (`LoadRooms`)

```sql
SELECT r.roomId, r.roomNumber, r.type, r.pricePerNight, r.capacity,
       r.isAvailable, r.description,
       (SELECT COUNT(*) FROM Bookings b
        WHERE b.roomId = r.roomId
          AND b.status IN ('CONFIRMED','CHECKED_IN')) AS occupiedCount
FROM Rooms r
ORDER BY r.roomNumber
```

`DisplayStatus` is derived in code:

```csharp
string status = occupiedCount > 0 ? "Occupied"
              : isAvailable       ? "Available"
              : "Maintenance";
```

---

## Adding a Room (`OnPostAdd`)

```csharp
// 1. Validate required fields
if (string.IsNullOrWhiteSpace(roomNumber) || string.IsNullOrWhiteSpace(type))
{
    ErrorMessage = "Room number and type are required.";
    ...
}

// 2. Check for duplicate room number
SELECT COUNT(*) FROM Rooms WHERE roomNumber = @rn
// count > 0 → ErrorMessage: "Room X already exists"

// 3. Insert
INSERT INTO Rooms (roomNumber, type, pricePerNight, capacity, isAvailable, description)
VALUES (@rn, @type, @price, @cap, 1, @desc)
```

- New rooms are always inserted with `isAvailable = 1`
- `type` is stored uppercase via `.ToUpper()`
- Room number uniqueness is checked in code before the DB insert (the DB also has a `UNIQUE` constraint as a safety net)

---

## Editing a Room (`OnPostEdit`)

```sql
UPDATE Rooms
SET roomNumber    = @rn,
    type          = @type,
    pricePerNight = @price,
    capacity      = @cap,
    description   = @desc
WHERE roomId = @id
```

- All fields can be updated except `roomId`
- `isAvailable` is not changed here — that's handled separately by the toggle

---

## Toggling Availability (`OnPostToggle`)

This is the maintenance toggle. Before flipping the flag, it checks if the room is currently occupied:

```sql
-- Block if occupied
SELECT COUNT(*) FROM Bookings
WHERE roomId = @id AND status = 'CHECKED_IN'
-- count > 0 → ErrorMessage: "Cannot change availability — room is currently occupied"

-- Flip the bit
UPDATE Rooms SET isAvailable = 1 - isAvailable WHERE roomId = @id
```

`1 - isAvailable` is a SQL trick to flip a BIT column:
- `1 - 1 = 0` (Available → Maintenance)
- `1 - 0 = 1` (Maintenance → Available)

A room with an active `CHECKED_IN` booking cannot be toggled — the guest is still in the room.

---

## Full Flow

```
Admin visits /Staff/Rooms
        │
        ├── Not ADMIN? → /Login
        │
        ▼
LoadRooms() → render room list with status badges

── Add Room ──────────────────────────────────────
Admin fills add form → POST OnPostAdd
        │
        ├── Missing fields? → ErrorMessage
        ├── Duplicate room number? → ErrorMessage
        └── INSERT → SuccessMessage → LoadRooms()

── Edit Room ─────────────────────────────────────
Admin fills edit form → POST OnPostEdit
        │
        └── UPDATE → SuccessMessage → LoadRooms()

── Toggle Availability ───────────────────────────
Admin clicks toggle → POST OnPostToggle
        │
        ├── Room occupied (CHECKED_IN)? → ErrorMessage
        └── UPDATE isAvailable = 1 - isAvailable → LoadRooms()
```
