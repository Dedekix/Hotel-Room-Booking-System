# Grand Haven Hotel — Room Booking System

## Overview
The **Grand Haven Hotel Room Booking System** is a web-based application built with **ASP.NET Core Razor Pages and C#** that allows customers to search, view, and book hotel rooms and events online.

The system provides a **customer portal**, a **staff dashboard**, and an **admin panel** to manage bookings, room availability, events, and hotel operations.

---

## Problem Statement
Traditional hotel booking methods require customers to call or physically visit the hotel to check availability. This leads to:
- Delays in booking
- Poor customer experience
- Risk of double bookings
- Difficulty managing records manually

---

## Objectives
- Enable customers to search and book rooms online
- Provide real-time room availability tracking
- Allow customers to browse and reserve hotel events
- Help staff manage check-ins and check-outs
- Allow admins to manage rooms, events, and monitor hotel performance

---

## Features

### Customer Module
- Register and login (email-based)
- OTP verification via email on every login
- Browse rooms with real-time availability status
- Book rooms — nightly or hourly
- Browse and reserve hotel events
- View booking and event reservation history
- Cancel confirmed bookings and event reservations

### Staff Module
- View live hotel metrics (revenue, occupancy, check-ins)
- Check-in and check-out guests
- View all room bookings and event bookings

### Admin Module
- All staff capabilities
- Add, edit, and toggle room availability
- Create, edit, and delete events (with image upload)
- Generate and export reports (Excel & PDF)
- Read customer contact messages

### Room Management
- Real-time availability derived from active bookings
- Double-booking prevention via date overlap check
- Maintenance mode per room (blocks new bookings)
- Supports four room types: Single, Double, Suite, Deluxe

### Event Management
- Capacity tracking (spots calculated dynamically)
- Image upload per event
- Duplicate booking prevention per user

---

## Technology Stack

| Layer | Technology |
|-------|-----------|
| Framework | ASP.NET Core Razor Pages (.NET 10) |
| Language | C# |
| Frontend | HTML5, CSS3, Bootstrap 5 |
| Database | Microsoft SQL Server (LocalDB) |
| Data Access | ADO.NET (`SqlConnection`, `SqlCommand`) |
| Messaging | Apache ActiveMQ 2.1.1 (Apache.NMS) |
| Email | Gmail SMTP via `System.Net.Mail` |
| OTP | Session-based, delivered via ActiveMQ + SMTP |
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
| `OtpCodes` | OTP schema (runtime OTP stored in session) |
| `Payments` | Payment tracking (schema defined, not yet implemented) |
| `Reports` | Report storage (schema defined, not yet implemented) |
| `ChatMessages` | Messaging (schema defined, not yet implemented) |

---

## Pages

| Page | Access | Description |
|------|--------|-------------|
| `/` (Index) | Public | Home page with featured events |
| `/Rooms` | Public | Browse all rooms with availability |
| `/BookRoom/{id}` | Customer | Book a specific room |
| `/BookingConfirmed` | Customer | Booking success confirmation |
| `/MyBookings` | Customer | View and cancel bookings & event reservations |
| `/Events` | Public | Browse and reserve events |
| `/BookEvent/{id}` | Customer | Reserve a specific event |
| `/Login` | Public | Email login with OTP for customers |
| `/VerifyOtp` | Customer | Enter OTP to complete login |
| `/Signup` | Public | Register a new customer account |
| `/Logout` | Authenticated | Clears session and redirects home |
| `/Contact` | Public | Submit a contact message |
| `/Staff/Dashboard` | Staff + Admin | Live metrics, check-in/check-out |
| `/Staff/Bookings` | Staff + Admin | All room bookings |
| `/Staff/EventBookings` | Staff + Admin | All event reservations |
| `/Staff/Rooms` | Admin only | Add, edit, toggle rooms |
| `/Staff/Events` | Admin only | Add, edit, delete events |
| `/Staff/Reports` | Admin only | Revenue reports, Excel/PDF export |
| `/Staff/Messages` | Admin only | Customer contact inbox |

---

## OTP Authentication Flow

Customers are required to verify their identity via a one-time password on every login:

```
Customer enters email → OTP generated → published to ActiveMQ queue
→ OtpEmailConsumer (background service) picks it up
→ sends OTP via Gmail SMTP → customer enters code on /VerifyOtp
→ session established → redirected to home
```

Staff and admins bypass OTP and log in directly.

See [`OTP_EXPLAINED.md`](./OTP_EXPLAINED.md) for the full breakdown.

---

## Validation & Business Rules
- Email must be unique per user
- Customers must verify identity via OTP on every login
- No double booking for the same room and overlapping dates
- Check-out date must be after check-in (enforced in code and DB constraint)
- Guest count must not exceed room capacity
- Only `CONFIRMED` bookings can be cancelled by customers
- Rooms with active `CHECKED_IN` guests cannot be toggled to maintenance
- Event capacity is tracked dynamically — cancellations free up spots

---

## Project Structure

```
├── Pages/
│   ├── Shared/          # Layouts (_Layout, _AdminLayout, _StaffLayout)
│   ├── Staff/           # Staff and admin pages
│   ├── Customer/        # Customer-only pages
│   │   ├── BookRoom.cshtml
│   │   ├── BookEvent.cshtml
│   │   ├── BookingConfirmed.cshtml
│   │   ├── MyBookings.cshtml
│   │   └── VerifyOtp.cshtml
│   ├── Login.cshtml     # Email login
│   ├── Signup.cshtml    # Registration
│   ├── Rooms.cshtml     # Room listing
│   ├── Events.cshtml    # Event listing
│   ├── Index.cshtml     # Home page
│   └── Contact.cshtml   # Contact form
├── Services/
│   ├── OtpEmailService.cs   # ActiveMQ OTP publisher
│   └── OtpEmailConsumer.cs  # Background email sender
├── wwwroot/
│   ├── css/             # Page-specific stylesheets
│   ├── Images/          # Room and event images
│   └── js/
├── docs/                # Feature implementation docs
├── appsettings.json     # DB, SMTP, and ActiveMQ config
├── database.txt         # SQL schema and seed data
└── OTP_EXPLAINED.md     # OTP system deep-dive
```

---

## Feature Documentation

Detailed implementation breakdowns for every feature are in the [`docs/`](./docs/DOC.md) folder.
