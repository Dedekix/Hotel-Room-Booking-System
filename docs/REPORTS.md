# Reports — How It Works

## Overview

The Reports page (`/Staff/Reports`) is **admin-only** and provides a summary of hotel performance metrics. Reports can be exported as **Excel (.xlsx)** or **PDF** files, generated on-demand using ClosedXML and iText7 respectively.

---

## Access Control

```csharp
var role = HttpContext.Session.GetString("UserRole");
if (role != "ADMIN")
    return Redirect("/Login?returnUrl=/Staff/Reports");
```

---

## Data Loaded (`LoadData`)

All data is fetched in a single query plus one grouped query:

### Summary Metrics

```sql
SELECT
    (SELECT ISNULL(SUM(totalPrice), 0) FROM Bookings WHERE status != 'CANCELLED') AS totalRevenue,
    (SELECT COUNT(*) FROM Bookings WHERE status != 'CANCELLED')                   AS totalBookings,
    (SELECT COUNT(*) FROM Rooms WHERE isAvailable = 0)                            AS occupiedRooms,
    (SELECT COUNT(*) FROM Rooms)                                                   AS totalRooms
```

Occupancy rate is calculated in code:
```csharp
OccupancyRate = TotalRooms > 0 ? $"{(OccupiedRooms * 100 / TotalRooms)}%" : "0%";
```

### Revenue by Room Type

```sql
SELECT r.type, COUNT(*) AS bookingCount, ISNULL(SUM(b.totalPrice), 0) AS revenue
FROM Bookings b
JOIN Rooms r ON r.roomId = b.roomId
WHERE b.status != 'CANCELLED'
GROUP BY r.type
ORDER BY revenue DESC
```

Groups bookings by room type and sums revenue — cancelled bookings excluded.

---

## Excel Export (`OnPostExportExcel`)

Uses **ClosedXML** to build a workbook in memory:

```csharp
using var wb = new XLWorkbook();
var ws = wb.Worksheets.Add("Report");

// Title row
ws.Cell(1, 1).Value = "Grand Haven Hotel - Report";
ws.Cell(1, 1).Style.Font.Bold = true;
ws.Range(1, 1, 1, 4).Merge();

// Summary section (rows 4-7)
ws.Cell(5, 1).Value = "Total Revenue";   ws.Cell(5, 2).Value = $"${TotalRevenue:N2}";
ws.Cell(6, 1).Value = "Total Bookings";  ws.Cell(6, 2).Value = TotalBookings;
ws.Cell(7, 1).Value = "Occupancy Rate";  ws.Cell(7, 2).Value = OccupancyRate;

// Revenue by room type table (rows 10+)
foreach (var r in RevenueByType)
{
    ws.Cell(row, 1).Value = r.RoomType;
    ws.Cell(row, 2).Value = r.BookingCount;
    ws.Cell(row, 3).Value = $"${r.Revenue:N2}";
}

ws.Columns().AdjustToContents();  // auto-fit column widths
```

The workbook is written to a `MemoryStream` and returned as a file download:

```csharp
return File(stream.ToArray(),
    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    $"GrandHaven_Report_{DateTime.Now:yyyyMMdd}.xlsx");
```

---

## PDF Export (`OnPostExportPdf`)

Uses **iText7** to build a PDF document in memory:

```csharp
var bold    = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
var regular = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
var gold    = new DeviceRgb(176, 122, 42);  // brand colour

using var stream = new MemoryStream();
var document = new Document(new PdfDocument(new PdfWriter(stream)));

// Title
document.Add(new Paragraph("Grand Haven Hotel — Report")
    .SetFont(bold).SetFontSize(20).SetFontColor(gold));

// Summary table (2 columns)
var summaryTable = new Table(2).UseAllAvailableWidth();
AddPdfRow(summaryTable, "Total Revenue",  $"${TotalRevenue:N2}", bold, regular);
AddPdfRow(summaryTable, "Total Bookings", TotalBookings.ToString(), bold, regular);
AddPdfRow(summaryTable, "Occupancy Rate", OccupancyRate, bold, regular);

// Revenue by type table (3 columns with gold header)
var table = new Table(3).UseAllAvailableWidth();
// headers: Room Type | Bookings | Revenue
// rows: one per room type
```

Returned as a file download:

```csharp
return File(stream.ToArray(), "application/pdf",
    $"GrandHaven_Report_{DateTime.Now:yyyyMMdd}.pdf");
```

---

## NuGet Packages Used

| Package     | Purpose                        |
|-------------|--------------------------------|
| ClosedXML   | Excel (.xlsx) generation       |
| itext7      | PDF generation                 |
| itext7.bouncy-castle-adapter | Cryptography support for iText7 |

---

## Full Flow

```
Admin visits /Staff/Reports
        │
        ├── Not ADMIN? → /Login
        │
        ▼
LoadData() → populate TotalRevenue, TotalBookings, OccupancyRate, RevenueByType
        │
        ▼
Render report page with metrics and table

── Export Excel ──────────────────────────────────
Admin clicks "Export Excel" → POST OnPostExportExcel
        │
LoadData() → build XLWorkbook in MemoryStream
        │
Return File(..., "GrandHaven_Report_YYYYMMDD.xlsx")
        │
Browser downloads the file

── Export PDF ────────────────────────────────────
Admin clicks "Export PDF" → POST OnPostExportPdf
        │
LoadData() → build iText7 Document in MemoryStream
        │
Return File(..., "GrandHaven_Report_YYYYMMDD.pdf")
        │
Browser downloads the file
```
