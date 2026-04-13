using ClosedXML.Excel;
using iText.IO.Font.Constants;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.SqlClient;

namespace HotelBookingSystem.Pages.Staff
{
    public class RevenueByType
    {
        public string RoomType     { get; set; } = "";
        public int    BookingCount { get; set; }
        public decimal Revenue     { get; set; }
    }

    public class ReportsModel : PageModel
    {
        private readonly string _conn;
        public ReportsModel(string connectionString) => _conn = connectionString;

        public decimal TotalRevenue    { get; set; }
        public int     TotalBookings   { get; set; }
        public int     OccupiedRooms   { get; set; }
        public int     TotalRooms      { get; set; }
        public string  OccupancyRate   { get; set; } = "0%";
        public List<RevenueByType> RevenueByType { get; set; } = new();

        public IActionResult OnGet()
        {
            var role = HttpContext.Session.GetString("UserRole");
            if (role != "ADMIN") return Redirect("/Login?returnUrl=/Staff/Reports");
            LoadData();
            return Page();
        }

        public IActionResult OnPostExportExcel()
        {
            LoadData();
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Report");

            // Title
            ws.Cell(1, 1).Value = "Grand Haven Hotel - Report";
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 16;
            ws.Range(1, 1, 1, 4).Merge();

            ws.Cell(2, 1).Value = $"Generated: {DateTime.Now:MMM d, yyyy h:mm tt}";
            ws.Range(2, 1, 2, 4).Merge();

            // Summary
            ws.Cell(4, 1).Value = "Summary";
            ws.Cell(4, 1).Style.Font.Bold = true;
            ws.Cell(5, 1).Value = "Total Revenue";   ws.Cell(5, 2).Value = $"${TotalRevenue:N2}";
            ws.Cell(6, 1).Value = "Total Bookings";  ws.Cell(6, 2).Value = TotalBookings;
            ws.Cell(7, 1).Value = "Occupancy Rate";  ws.Cell(7, 2).Value = OccupancyRate;

            // Revenue by Room Type
            ws.Cell(9, 1).Value = "Revenue by Room Type";
            ws.Cell(9, 1).Style.Font.Bold = true;
            ws.Cell(10, 1).Value = "Room Type";
            ws.Cell(10, 2).Value = "Bookings";
            ws.Cell(10, 3).Value = "Revenue";
            ws.Row(10).Style.Font.Bold = true;

            int row = 11;
            foreach (var r in RevenueByType)
            {
                ws.Cell(row, 1).Value = r.RoomType;
                ws.Cell(row, 2).Value = r.BookingCount;
                ws.Cell(row, 3).Value = $"${r.Revenue:N2}";
                row++;
            }

            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            wb.SaveAs(stream);
            stream.Position = 0;
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"GrandHaven_Report_{DateTime.Now:yyyyMMdd}.xlsx");
        }

        public IActionResult OnPostExportPdf()
        {
            LoadData();
            var bold    = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            var regular = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
            var gold    = new DeviceRgb(176, 122, 42);

            using var stream = new MemoryStream();
            var writer   = new PdfWriter(stream);
            var pdf      = new PdfDocument(writer);
            var document = new Document(pdf);

            // Title
            document.Add(new Paragraph("Grand Haven Hotel \u2014 Report")
                .SetFont(bold).SetFontSize(20).SetFontColor(gold));

            document.Add(new Paragraph($"Generated: {DateTime.Now:MMM d, yyyy h:mm tt}")
                .SetFont(regular).SetFontSize(10).SetFontColor(ColorConstants.GRAY));

            document.Add(new Paragraph(" "));

            // Summary
            document.Add(new Paragraph("Summary").SetFont(bold).SetFontSize(14));

            var summaryTable = new iText.Layout.Element.Table(2).UseAllAvailableWidth();
            AddPdfRow(summaryTable, "Total Revenue",  $"${TotalRevenue:N2}", bold, regular);
            AddPdfRow(summaryTable, "Total Bookings", TotalBookings.ToString(), bold, regular);
            AddPdfRow(summaryTable, "Occupancy Rate", OccupancyRate, bold, regular);
            document.Add(summaryTable);

            document.Add(new Paragraph(" "));

            // Revenue by Room Type
            document.Add(new Paragraph("Revenue by Room Type").SetFont(bold).SetFontSize(14));

            var table = new iText.Layout.Element.Table(3).UseAllAvailableWidth();
            foreach (var header in new[] { "Room Type", "Bookings", "Revenue" })
                table.AddHeaderCell(new Cell().Add(new Paragraph(header).SetFont(bold))
                    .SetBackgroundColor(gold)
                    .SetFontColor(ColorConstants.WHITE));

            foreach (var r in RevenueByType)
            {
                table.AddCell(new Paragraph(r.RoomType).SetFont(regular));
                table.AddCell(new Paragraph(r.BookingCount.ToString()).SetFont(regular));
                table.AddCell(new Paragraph($"${r.Revenue:N2}").SetFont(regular));
            }
            document.Add(table);
            document.Close();

            return File(stream.ToArray(), "application/pdf",
                $"GrandHaven_Report_{DateTime.Now:yyyyMMdd}.pdf");
        }

        private void AddPdfRow(iText.Layout.Element.Table table, string label, string value, PdfFont bold, PdfFont regular)
        {
            table.AddCell(new Cell().Add(new Paragraph(label).SetFont(bold)));
            table.AddCell(new Cell().Add(new Paragraph(value).SetFont(regular)));
        }

        private void LoadData()
        {
            using var conn = new SqlConnection(_conn);
            conn.Open();

            string sql = @"
                SELECT
                    (SELECT ISNULL(SUM(totalPrice), 0) FROM Bookings WHERE status != 'CANCELLED') AS totalRevenue,
                    (SELECT COUNT(*) FROM Bookings WHERE status != 'CANCELLED')                   AS totalBookings,
                    (SELECT COUNT(*) FROM Rooms WHERE isAvailable = 0)                            AS occupiedRooms,
                    (SELECT COUNT(*) FROM Rooms)                                                   AS totalRooms";

            using (var cmd = new SqlCommand(sql, conn))
            using (var r = cmd.ExecuteReader())
            {
                if (r.Read())
                {
                    TotalRevenue  = (decimal)r["totalRevenue"];
                    TotalBookings = (int)r["totalBookings"];
                    OccupiedRooms = (int)r["occupiedRooms"];
                    TotalRooms    = (int)r["totalRooms"];
                    OccupancyRate = TotalRooms > 0
                        ? $"{(OccupiedRooms * 100 / TotalRooms)}%"
                        : "0%";
                }
            }

            string revSql = @"
                SELECT r.type, COUNT(*) AS bookingCount, ISNULL(SUM(b.totalPrice), 0) AS revenue
                FROM Bookings b
                JOIN Rooms r ON r.roomId = b.roomId
                WHERE b.status != 'CANCELLED'
                GROUP BY r.type
                ORDER BY revenue DESC";

            using (var cmd = new SqlCommand(revSql, conn))
            using (var r = cmd.ExecuteReader())
                while (r.Read())
                    RevenueByType.Add(new RevenueByType
                    {
                        RoomType     = r["type"].ToString()!,
                        BookingCount = (int)r["bookingCount"],
                        Revenue      = (decimal)r["revenue"]
                    });
        }
    }
}
