-- Delete related records first, then users
-- UserIds: 1 (Admin), 3 (test 1), 4 (Ariane), 5 (Noah), 6 (Christophe), 7 (Ingabire)

DELETE FROM EventBookings WHERE userId IN (1, 3, 4, 5, 6, 7);
DELETE FROM Bookings      WHERE userId IN (1, 3, 4, 5, 6, 7);
DELETE FROM OtpCodes      WHERE userId IN (1, 3, 4, 5, 6, 7);
DELETE FROM Users         WHERE userId IN (1, 3, 4, 5, 6, 7);
