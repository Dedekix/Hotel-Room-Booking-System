# Staff Dashboard — How It Works

## Overview

The Staff Dashboard (`/Staff/Dashboard`) is the main landing page for both STAFF and ADMIN users after login. It shows live hotel metrics and a list of active/upcoming guests, with the ability to check guests in and out directly from the dashboard.

---

## Access Control

```csharp
var role = HttpContext.Session.GetString("UserRole");
if (role != "STAFF" && role != "ADMIN")
    return Redirect("/Login?returnUrl=/Staff/Dashboard");
```

Both STAFF and ADMIN can access this page. Customers are redirected to login.

---

## Dashboard Metrics

All metrics are loaded from the DB on every page load using a shared `Scalar<T>` helper:

```csharp
private static T Scalar<T>(SqlConnection conn, string sql)
{
    using var cmd = new SqlCommand(sql, conn);
    var result = cmd.ExecuteScalar();
    return result == null || result == DBNull.Value ? default! : (T)Convert.ChangeType(result, typeof(T));
}
```

### Total Bookings
```sql
SELECT COUNT(*) FROM Bookings WHERE status NOT IN ('CANCELLED')
```
All non-cancelled bookings ever made.

### Today's Check-ins (Currently In-House)
```sql
SELECT COUNT(*) FROM Bookings WHERE status = 'CHECKED_IN'
```
Guests currently checked in (not just today's arrivals — all active stays).

### Total Revenue
```sql
SELECT ISNULL(SUM(totalPrice), 0)
FROM Bookings
WHERE status IN ('CONFIRMED','CHECKED_IN','CHECKED_OUT')
```
Sum of all revenue from confirmed, active, and completed bookings. Cancelled bookings are excluded.

### Occupancy Rate
```csharp
int occupiedRooms    = Scalar<int>(conn, "SELECT COUNT(*) FROM Bookings WHERE status = 'CHECKED_IN'");
OccupancyPercent     = TotalRooms > 0 ? (int)Math.Round(occupiedRooms * 100.0 / TotalRooms) : 0;
```
`(rooms with CHECKED_IN bookings ÷ total rooms) × 100`

### Pending Messages
```sql
SELECT COUNT(*) FROM ContactMessages WHERE isRead = 0
```
Unread contact form submissions — shown as a badge to prompt staff attention.

---

## Today's Activity List

```sql
SELECT b.bookingId, u.fullName, r.roomNumber, b.status, b.checkInDate, b.checkOutDate
FROM Bookings b
JOIN Users u ON b.userId = u.userId
JOIN Rooms r ON b.roomId = r.roomId
WHERE b.status IN ('CHECKED_IN', 'CONFIRMED')
ORDER BY b.checkInDate ASC
```

Shows all guests who are either:
- Currently checked in (`CHECKED_IN`)
- Have upcoming confirmed bookings (`CONFIRMED`)

Ordered by check-in date so the most imminent arrivals appear first.

---

## Check-In / Check-Out Actions

Both actions use the same private helper:

```csharp
private void UpdateStatus(int bookingId, string status)
{
    using var conn = new SqlConnection(_conn);
    conn.Open();
    using var cmd = new SqlCommand(
        "UPDATE Bookings SET status = @s WHERE bookingId = @id", conn);
    cmd.Parameters.AddWithValue("@s",  status);
    cmd.Parameters.AddWithValue("@id", bookingId);
    cmd.ExecuteNonQuery();
}
```

### Check-In
```csharp
public IActionResult OnPostCheckIn(int bookingId)
{
    UpdateStatus(bookingId, "CHECKED_IN");
    return RedirectToPage();
}
```
Changes status from `CONFIRMED` → `CHECKED_IN`.

### Check-Out
```csharp
public IActionResult OnPostCheckOut(int bookingId)
{
    UpdateStatus(bookingId, "CHECKED_OUT");
    return RedirectToPage();
}
```
Changes status from `CHECKED_IN` → `CHECKED_OUT`. Once checked out, the booking disappears from the activity list (only `CONFIRMED` and `CHECKED_IN` are shown).

After either action, `RedirectToPage()` reloads the dashboard with fresh data.

---

## Full Flow

```
Staff/Admin visits /Staff/Dashboard
        │
        ├── Not STAFF or ADMIN? → /Login
        │
        ▼
Run 5 scalar queries → populate metrics
Run activity query → populate TodayActivity list
        │
        ▼
Render dashboard with:
  - KPI cards (bookings, check-ins, revenue, occupancy, messages)
  - Activity table with Check-In / Check-Out buttons
        │
        ▼
Staff clicks Check-In or Check-Out
        │
POST → OnPostCheckIn / OnPostCheckOut
        │
UPDATE Bookings SET status = @s WHERE bookingId = @id
        │
        ▼
RedirectToPage() → dashboard reloads with updated data
```
