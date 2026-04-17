# Room Listing & Availability — How It Works

## Overview

The Rooms page (`/Rooms`) displays all hotel rooms with their real-time availability status. It does not require login — any visitor can browse rooms. Status is derived dynamically from the `Bookings` table rather than a static flag.

---

## Database Tables Involved

```sql
-- Rooms table
CREATE TABLE Rooms (
    roomId        INT IDENTITY(1,1) PRIMARY KEY,
    roomNumber    VARCHAR(10)    NOT NULL UNIQUE,
    type          VARCHAR(10)    NOT NULL CHECK (type IN ('SINGLE', 'DOUBLE', 'SUITE', 'DELUXE')),
    pricePerNight DECIMAL(10,2)  NOT NULL,
    capacity      INT            NOT NULL,
    isAvailable   BIT            NOT NULL DEFAULT 1,
    description   VARCHAR(255)
);
```

`isAvailable` is a manual flag set by admins (e.g. for maintenance). Actual occupancy is determined by checking active bookings.

---

## Data Model (`RoomDisplay`)

```csharp
public class RoomDisplay
{
    public int      RoomId        { get; set; }
    public string   RoomNumber    { get; set; }
    public string   Type          { get; set; }
    public decimal  PricePerNight { get; set; }
    public int      Capacity      { get; set; }
    public bool     IsAvailable   { get; set; }
    public string   Description   { get; set; }
    public string   DisplayStatus { get; set; }   // "Available" | "Occupied" | "Maintenance"
    public string   ImagePath     { get; set; }
    public DateTime? AvailableFrom { get; set; }  // earliest checkout date of active bookings
}
```

---

## The Query

```sql
SELECT r.roomId, r.roomNumber, r.type, r.pricePerNight, r.capacity,
       r.isAvailable, r.description,
       (SELECT COUNT(*) FROM Bookings b
        WHERE b.roomId = r.roomId
          AND b.status IN ('CONFIRMED','CHECKED_IN')) AS occupiedCount,
       (SELECT MIN(b.checkOutDate) FROM Bookings b
        WHERE b.roomId = r.roomId
          AND b.status IN ('CONFIRMED','CHECKED_IN')) AS availableFrom
FROM Rooms r
ORDER BY r.roomNumber
```

Two correlated subqueries run per room:
- `occupiedCount` — counts active bookings (CONFIRMED or CHECKED_IN)
- `availableFrom` — the earliest checkout date, so the UI can show "Available from May 10"

---

## Status Logic

```csharp
string status = occupiedCount > 0 ? "Occupied"
              : isAvailable       ? "Available"
              : "Maintenance";
```

| Condition                        | Status        |
|----------------------------------|---------------|
| Has active bookings              | Occupied      |
| No active bookings + isAvailable | Available     |
| No active bookings + !isAvailable| Maintenance   |

---

## Image Mapping

Images are assigned per room type since there are no per-room images in the DB:

```csharp
string img = type.ToUpper() switch
{
    "SUITE"  => "Images/room301.jpg",
    "DELUXE" => "Images/room201.jpg",
    "SINGLE" => "Images/Single room.jpg",
    _        => "Images/room101.jpg"   // DOUBLE and fallback
};
```

---

## Room Types & Pricing (Seed Data)

| Room Number | Type   | Price/Night | Capacity |
|-------------|--------|-------------|----------|
| 101, 102    | SINGLE | $89.99      | 1        |
| 201, 202    | DOUBLE | $149.99     | 2        |
| 301, 302    | SUITE  | $299.99     | 3        |
| 401, 402    | DELUXE | $199.99     | 2        |

---

## Booking Button Behaviour

- If a room's `DisplayStatus` is `"Available"` → "Book Now" button links to `/BookRoom/{roomId}`
- If `"Occupied"` → button is disabled, shows `AvailableFrom` date if available
- If `"Maintenance"` → button is disabled, shows "Under Maintenance"
