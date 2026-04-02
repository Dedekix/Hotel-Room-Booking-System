using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace HotelBookingSystem.Pages
{
    public class RoomsModel : PageModel
    {
        string connectionString = "Data Source=DELPHINE\\SQLEXPRESS;Initial Catalog=HotelBookingSystemDB;Integrated Security=True;Trust Server Certificate=True";

        public List<RoomList> Rooms { get; set; } = new List<RoomList>();
        public DateTime CheckDateTime { get; set; } = DateTime.Now;
        public string BookingType { get; set; } = "Daily";
        public int Duration { get; set; } = 1;

        public void OnGet(DateTime? checkDateTime, string bookingType, int? duration)
        {
            if (checkDateTime.HasValue) CheckDateTime = checkDateTime.Value;
            if (!string.IsNullOrEmpty(bookingType)) BookingType = bookingType;
            if (duration.HasValue) Duration = duration.Value;

            LoadRooms();
        }

        private void LoadRooms()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    string query = "SELECT Id, RoomNumber, RoomType, PricePerNight, PricePerHour, Capacity, Description, Amenities, IsAvailable FROM Rooms WHERE IsActive = 1 ORDER BY RoomNumber";
                    conn.Open();

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                RoomList room = new RoomList();
                                room.Id = int.Parse(reader["Id"].ToString());
                                room.RoomNumber = reader["RoomNumber"].ToString();
                                room.RoomType = reader["RoomType"].ToString();
                                room.PricePerNight = decimal.Parse(reader["PricePerNight"].ToString());
                                room.PricePerHour = decimal.Parse(reader["PricePerHour"].ToString());
                                room.Capacity = int.Parse(reader["Capacity"].ToString());
                                room.Description = reader["Description"].ToString();
                                room.Amenities = reader["Amenities"].ToString();
                                room.IsAvailable = bool.Parse(reader["IsAvailable"].ToString());
                                Rooms.Add(room);
                            }
                        }
                    }
                    conn.Close();
                }

                // Check availability for each room
                CheckRoomAvailability();
            }
            catch (Exception ex)
            {
                // Handle error
            }
        }

        private void CheckRoomAvailability()
        {
            DateTime checkOutDateTime;
            if (BookingType == "Daily")
            {
                checkOutDateTime = CheckDateTime.AddDays(Duration);
            }
            else
            {
                checkOutDateTime = CheckDateTime.AddHours(Duration);
            }

            foreach (var room in Rooms)
            {
                try
                {
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    {
                        string query = @"SELECT COUNT(*) FROM Bookings 
                                        WHERE RoomId = @roomId 
                                        AND Status NOT IN ('Cancelled', 'CheckedOut')
                                        AND CheckInDate <= @checkOutDateTime 
                                        AND CheckOutDateTime >= @checkInDateTime";

                        conn.Open();
                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@roomId", room.Id);
                            cmd.Parameters.AddWithValue("@checkOutDateTime", checkOutDateTime);
                            cmd.Parameters.AddWithValue("@checkInDateTime", CheckDateTime);

                            int conflictCount = (int)cmd.ExecuteScalar();
                            room.IsAvailable = conflictCount == 0;

                            if (!room.IsAvailable)
                            {
                                // Get next available date
                                string nextQuery = @"SELECT MAX(CheckOutDateTime) FROM Bookings 
                                                    WHERE RoomId = @roomId AND Status NOT IN ('Cancelled', 'CheckedOut')
                                                    AND CheckOutDateTime > @checkInDateTime";

                                using (SqlCommand nextCmd = new SqlCommand(nextQuery, conn))
                                {
                                    nextCmd.Parameters.AddWithValue("@roomId", room.Id);
                                    nextCmd.Parameters.AddWithValue("@checkInDateTime", CheckDateTime);
                                    object result = nextCmd.ExecuteScalar();
                                    if (result != DBNull.Value)
                                    {
                                        room.NextAvailableDate = Convert.ToDateTime(result);
                                    }
                                }
                            }
                        }
                        conn.Close();
                    }
                }
                catch (Exception ex)
                {
                    // Handle error
                }
            }
        }

        public IActionResult OnPostBook(int roomId, string bookingType, DateTime checkDateTime, int duration)
        {
            try
            {
                string userId = HttpContext.Session.GetString("UserId");
                if (string.IsNullOrEmpty(userId))
                {
                    return RedirectToPage("/Login");
                }

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    // Get room details
                    string roomQuery = "SELECT PricePerNight, PricePerHour FROM Rooms WHERE Id = @roomId";
                    conn.Open();

                    decimal pricePerNight = 0;
                    decimal pricePerHour = 0;

                    using (SqlCommand roomCmd = new SqlCommand(roomQuery, conn))
                    {
                        roomCmd.Parameters.AddWithValue("@roomId", roomId);
                        using (SqlDataReader reader = roomCmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                pricePerNight = decimal.Parse(reader["PricePerNight"].ToString());
                                pricePerHour = decimal.Parse(reader["PricePerHour"].ToString());
                            }
                            reader.Close();
                        }
                    }

                    DateTime checkOutDateTime;
                    decimal totalPrice;
                    string bookingReference = "BOOK-" + DateTime.Now.ToString("yyyyMMddHHmmss");

                    if (bookingType == "Daily")
                    {
                        checkOutDateTime = checkDateTime.AddDays(duration);
                        totalPrice = pricePerNight * duration;
                    }
                    else
                    {
                        checkOutDateTime = checkDateTime.AddHours(duration);
                        totalPrice = pricePerHour * duration;
                    }

                    // Insert booking
                    string insertQuery = @"INSERT INTO Bookings (BookingReference, UserId, RoomId, BookingType, CheckInDate, CheckInTime, CheckOutDateTime, Duration, Guests, TotalPrice, Status, BookingDate) 
                                           VALUES (@bookingRef, @userId, @roomId, @bookingType, @checkInDate, @checkInTime, @checkOutDateTime, @duration, 1, @totalPrice, 'Confirmed', GETDATE())";

                    using (SqlCommand insertCmd = new SqlCommand(insertQuery, conn))
                    {
                        insertCmd.Parameters.AddWithValue("@bookingRef", bookingReference);
                        insertCmd.Parameters.AddWithValue("@userId", int.Parse(userId));
                        insertCmd.Parameters.AddWithValue("@roomId", roomId);
                        insertCmd.Parameters.AddWithValue("@bookingType", bookingType);
                        insertCmd.Parameters.AddWithValue("@checkInDate", checkDateTime.Date);
                        insertCmd.Parameters.AddWithValue("@checkInTime", checkDateTime.TimeOfDay);
                        insertCmd.Parameters.AddWithValue("@checkOutDateTime", checkOutDateTime);
                        insertCmd.Parameters.AddWithValue("@duration", duration);
                        insertCmd.Parameters.AddWithValue("@totalPrice", totalPrice);
                        insertCmd.ExecuteNonQuery();
                    }

                    conn.Close();
                }

                TempData["SuccessMessage"] = "Room booked successfully!";
                return RedirectToPage("/MyBookings");
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Booking failed. Please try again.";
                return RedirectToPage("/Rooms");
            }
        }
    }

    public class RoomList
    {
        public int Id { get; set; }
        public string RoomNumber { get; set; }
        public string RoomType { get; set; }
        public decimal PricePerNight { get; set; }
        public decimal PricePerHour { get; set; }
        public int Capacity { get; set; }
        public string Description { get; set; }
        public string Amenities { get; set; }
        public bool IsAvailable { get; set; }
        public DateTime? NextAvailableDate { get; set; }
    }
}