# Hotel Room Booking System

## Overview
The **Hotel Room Booking System** is a web-based application developed using **ASP.NET Web Forms and C#** that allows customers to easily search, view, and book hotel rooms online.  

The system also provides dashboards for **hotel staff** and **administrators** to efficiently manage bookings, room availability, and hotel operations.

This project replaces manual booking processes with a **simple, user-friendly, and efficient digital solution**.

---

## Problem Statement
Traditional hotel booking methods often require customers to call or physically visit the hotel to check availability. This leads to:
- Delays in booking  
- Poor customer experience  
- Risk of **double bookings**  
- Difficulty managing records manually  

---

## Objectives
- Enable customers to **search and book rooms online**
- Provide real-time **room availability tracking**
- Help staff manage **check-ins and check-outs**
- Allow admins to manage rooms and monitor **hotel performance**

---

## Features

### Customer Module
- Register and login  
- Search rooms by check-in/check-out dates  
- View room details (type, price, capacity)  
- Book rooms online  
- View booking history (past & upcoming)  
- Cancel bookings  

### Staff Module
- View today's and upcoming bookings  
- Check-in guests  
- Check-out guests  
- Monitor room occupancy  

### Admin Module
- Add, edit, and delete rooms  
- Manage room types and pricing  
- View all bookings  
- Generate basic reports (revenue & occupancy)  

### Room Management
- Track room availability  
- Prevent double bookings  
- Support multiple room types (Single, Double, Suite, Deluxe)  

---

## Database Design

The system uses **Microsoft SQL Server** with the following main tables:

- **Users** – Stores customer, staff, and admin details  
- **Rooms** – Stores room information and availability  
- **Bookings** – Handles all reservation data  
- **Payments (Optional)** – Tracks transactions  

---

## Technology Stack

| Layer | Technology |
|------|-----------|
| Frontend | ASP.NET Web Forms, HTML5, CSS3, Bootstrap 5 |
| Backend | C# (.NET Framework 4.8) |
| Database | Microsoft SQL Server Express |
| Data Access | ADO.NET / Entity Framework |
| Authentication | Session-based (Role-based access) |
| Hosting | IIS / Azure (optional) |

---

## System Modules
- Authentication System (Login/Register)  
- Room Search & Availability  
- Booking Management  
- Customer Dashboard  
- Staff Dashboard  
- Admin Panel  
- Reporting System (Optional)  

---

## User Interfaces
- Home Page  
- Login & Registration Pages  
- Customer Dashboard  
- Room Listing & Details Pages  
- Booking Page  
- My Bookings Page  
- Staff Dashboard  
- Admin Dashboard  

---

## Validation & Business Rules
- Email must be unique  
- No double booking for the same room and dates  
- Check-out date must be after check-in  
- Guest count must not exceed room capacity  
- Booking cancellation restrictions apply  

