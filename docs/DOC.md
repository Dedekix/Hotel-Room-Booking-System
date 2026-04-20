# Grand Haven Hotel — Feature Documentation Index

This folder contains detailed implementation breakdowns for every feature in the system.

---

## Docs

| File | Feature |
|------|---------|
| [AUTHENTICATION.md](./AUTHENTICATION.md) | Signup, Login, Logout, Session management, Role-based access |
| [OTP_EXPLAINED.md](./OTP_EXPLAINED.md) | OTP generation, ActiveMQ messaging, Gmail SMTP delivery |
| [ROOM_LISTING.md](./ROOM_LISTING.md) | Room availability display, status logic, image mapping |
| [ROOM_BOOKING.md](./ROOM_BOOKING.md) | Booking flow, nightly vs hourly pricing, double-booking prevention |
| [MY_BOOKINGS.md](./MY_BOOKINGS.md) | Customer booking history, event reservations, cancellation |
| [STAFF_DASHBOARD.md](./STAFF_DASHBOARD.md) | Live metrics, activity list, check-in/check-out actions |
| [ADMIN_ROOM_MANAGEMENT.md](./ADMIN_ROOM_MANAGEMENT.md) | Add/edit rooms, availability toggle, maintenance mode |
| [EVENTS.md](./EVENTS.md) | Event browsing, booking, capacity tracking, admin CRUD |
| [REPORTS.md](./REPORTS.md) | Revenue metrics, Excel export (ClosedXML), PDF export (iText7) |
| [CONTACT_MESSAGES.md](./CONTACT_MESSAGES.md) | Contact form submission, admin inbox, mark as read |
| [PAYMENT.md](./PAYMENT.md) | Payment flow for room bookings and event reservations, online vs in-person |
| [CHAT.md](./CHAT.md) | Live chat — customer ↔ staff messaging, file attachments, polling, unread badges |

---

## Technology Stack

| Layer | Technology |
|-------|-----------|
| Framework | ASP.NET Core Razor Pages (.NET 10) |
| Database | Microsoft SQL Server (LocalDB) |
| Data Access | ADO.NET (`SqlConnection`, `SqlCommand`) |
| Messaging | Apache ActiveMQ 2.1.1 (NMS) |
| Email | Gmail SMTP via `System.Net.Mail` |
| Excel Export | ClosedXML |
| PDF Export | iText7 + iText7.bouncy-castle-adapter |
| Authentication | Session-based (role-based access control) |

---

## Database Tables

| Table | Purpose |
|-------|---------|
| `Users` | Customers, staff, and admins |
| `Rooms` | Room inventory and availability |
| `Bookings` | Room reservations |
| `Events` | Hotel events |
| `EventBookings` | Event reservations |
| `ContactMessages` | Customer contact form submissions |
| `OtpCodes` | (Schema defined, OTP stored in session at runtime) |
| `Payments` | Payment records for room bookings and event reservations |
| `Reports` | Report storage (schema defined, not yet implemented) |
| `ChatMessages` | Live chat messages between customers and staff (with file attachments) |
