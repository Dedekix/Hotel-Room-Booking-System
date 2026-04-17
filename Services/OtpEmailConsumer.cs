using Apache.NMS;
using Apache.NMS.ActiveMQ;
using System.Net;
using System.Net.Mail;

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
            var dest          = session.GetQueue(_queueName);
            using var consumer = session.CreateConsumer(dest);
            conn.Start();

            while (!stoppingToken.IsCancellationRequested)
            {
                var msg = consumer.Receive(TimeSpan.FromSeconds(2)) as ITextMessage;
                if (msg != null)
                {
                    var parts = msg.Text.Split('|');
                    if (parts.Length == 2)
                        await SendEmailAsync(parts[0], parts[1]);
                }
            }
        }

        private async Task SendEmailAsync(string toEmail, string otp)
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
    }
}
