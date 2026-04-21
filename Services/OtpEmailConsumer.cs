using Apache.NMS;
using Apache.NMS.ActiveMQ;
using iText.IO.Font.Constants;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;

namespace HotelBookingSystem.Services
{
    public class OtpEmailConsumer : BackgroundService
    {
        private readonly string _brokerUri;
        private readonly string _queueName;
        private readonly string _smtpHost;
        private readonly int    _smtpPort;
        private readonly string _emailUser;
        private readonly string _emailPass;

        public OtpEmailConsumer(IConfiguration config)
        {
            _brokerUri = config["ActiveMQ:BrokerUri"]!;
            _queueName = config["ActiveMQ:OtpQueue"]!;
            _smtpHost  = config["Email:SmtpHost"]!;
            _smtpPort  = int.Parse(config["Email:SmtpPort"]!);
            _emailUser = config["Email:Username"]!;
            _emailPass = config["Email:Password"]!;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var factory = new ConnectionFactory(_brokerUri);
            using var conn    = factory.CreateConnection();
            using var session = conn.CreateSession(AcknowledgementMode.AutoAcknowledge);

            var otpDest      = session.GetQueue(_queueName);
            var receiptDest  = session.GetQueue("receipt.queue");

            using var otpConsumer     = session.CreateConsumer(otpDest);
            using var receiptConsumer = session.CreateConsumer(receiptDest);
            conn.Start();

            while (!stoppingToken.IsCancellationRequested)
            {
                var otpMsg = otpConsumer.Receive(TimeSpan.FromSeconds(1)) as ITextMessage;
                if (otpMsg != null)
                {
                    var parts = otpMsg.Text.Split('|');
                    if (parts.Length == 2)
                        await SendOtpEmailAsync(parts[0], parts[1]);
                }

                var receiptMsg = receiptConsumer.Receive(TimeSpan.FromSeconds(1)) as ITextMessage;
                if (receiptMsg != null)
                {
                    var parts = receiptMsg.Text.Split('|');
                    if (parts.Length == 4)
                        await SendReceiptEmailAsync(parts[0], parts[1], parts[2], decimal.Parse(parts[3]));
                }
            }
        }

        private async Task SendOtpEmailAsync(string toEmail, string otp)
        {
            using var smtp = new SmtpClient(_smtpHost, _smtpPort)
            {
                Credentials = new NetworkCredential(_emailUser, _emailPass),
                EnableSsl   = true
            };

            var mail = new MailMessage(_emailUser, toEmail)
            {
                Subject = "Your Grand Haven OTP Code",
                Body    = $"Your one-time password is: {otp}\n\nThis code expires in 5 minutes."
            };

            await smtp.SendMailAsync(mail);
        }

        private async Task SendReceiptEmailAsync(string toEmail, string bookingType, string summary, decimal amount)
        {
            string type    = bookingType == "ROOM" ? "Room Booking" : "Event Reservation";
            string amtLine = amount == 0 ? "Free" : $"${amount:0.00}";
            string refNo   = $"GH-{DateTime.Now:yyyyMMddHHmmss}";

            // ── Generate PDF receipt ──────────────────────────────
            byte[] pdfBytes;
            using (var ms = new MemoryStream())
            {
                var writer   = new PdfWriter(ms);
                var pdf      = new PdfDocument(writer);
                var doc      = new Document(pdf);
                var gold     = new DeviceRgb(176, 122, 42);
                var dark     = new DeviceRgb(26, 26, 26);
                var light    = new DeviceRgb(253, 248, 242);
                var bold     = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
                var regular  = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

                // Header bar
                var header = new Paragraph("GRAND HAVEN HOTEL")
                    .SetFont(bold).SetFontSize(20).SetFontColor(ColorConstants.WHITE)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetBackgroundColor(dark)
                    .SetPadding(16);
                doc.Add(header);

                doc.Add(new Paragraph("Booking Receipt")
                    .SetFont(bold).SetFontSize(13).SetFontColor(gold)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetMarginTop(4).SetMarginBottom(16));

                // Receipt details table
                var table = new Table(new float[] { 2, 3 }).UseAllAvailableWidth()
                    .SetBackgroundColor(light).SetBorderRadius(new BorderRadius(6));

                void AddRow(string label, string value)
                {
                    table.AddCell(new Cell().Add(new Paragraph(label).SetFont(bold).SetFontSize(11).SetFontColor(dark))
                        .SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetPadding(8));
                    table.AddCell(new Cell().Add(new Paragraph(value).SetFont(regular).SetFontSize(11))
                        .SetBorder(iText.Layout.Borders.Border.NO_BORDER).SetPadding(8));
                }

                AddRow("Reference",   refNo);
                AddRow("Type",        type);
                AddRow("Details",     summary);
                AddRow("Amount",      amtLine);
                AddRow("Status",      "CONFIRMED");
                AddRow("Issued",      DateTime.Now.ToString("MMM d, yyyy h:mm tt"));

                doc.Add(table);

                // Amount highlight
                doc.Add(new Paragraph($"Total Paid: {amtLine}")
                    .SetFont(bold).SetFontSize(16).SetFontColor(gold)
                    .SetTextAlignment(TextAlignment.RIGHT)
                    .SetMarginTop(16));

                // Footer
                doc.Add(new Paragraph("Thank you for choosing Grand Haven Hotel. We look forward to welcoming you.")
                    .SetFont(regular).SetFontSize(10).SetFontColor(ColorConstants.GRAY)
                    .SetTextAlignment(TextAlignment.CENTER)
                    .SetMarginTop(24));

                doc.Close();
                pdfBytes = ms.ToArray();
            }

            // ── Send email with PDF attachment ────────────────────
            using var smtp = new SmtpClient(_smtpHost, _smtpPort)
            {
                Credentials = new NetworkCredential(_emailUser, _emailPass),
                EnableSsl   = true
            };

            string body = $"Dear Guest,\n\nYour booking has been confirmed. Please find your receipt attached.\n\n" +
                          $"  Reference : {refNo}\n" +
                          $"  Type      : {type}\n" +
                          $"  Details   : {summary}\n" +
                          $"  Amount    : {amtLine}\n" +
                          $"  Status    : CONFIRMED\n\n" +
                          $"Warm regards,\nGrand Haven Hotel";

            var mail = new MailMessage(_emailUser, toEmail)
            {
                Subject    = $"Grand Haven \u2014 Booking Confirmed ({type})",
                Body       = body,
                IsBodyHtml = false
            };

            var attachment = new Attachment(
                new MemoryStream(pdfBytes),
                $"GrandHaven_Receipt_{refNo}.pdf",
                MediaTypeNames.Application.Pdf);
            mail.Attachments.Add(attachment);

            await smtp.SendMailAsync(mail);
        }
    }
}
