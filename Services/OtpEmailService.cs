using Apache.NMS;
using Apache.NMS.ActiveMQ;

namespace HotelBookingSystem.Services
{
    public class OtpEmailService
    {
        private readonly string _brokerUri;
        private readonly string _queueName;

        public OtpEmailService(IConfiguration config)
        {
            _brokerUri = config["ActiveMQ:BrokerUri"]!;
            _queueName = config["ActiveMQ:OtpQueue"]!;
        }

        public void PublishOtp(string toEmail, string otp)
        {
            var factory = new ConnectionFactory(_brokerUri);
            using var conn    = factory.CreateConnection();
            using var session = conn.CreateSession(AcknowledgementMode.AutoAcknowledge);
            var dest          = session.GetQueue(_queueName);
            using var producer = session.CreateProducer(dest);
            conn.Start();

            producer.Send(session.CreateTextMessage($"{toEmail}|{otp}"));
        }

        public void PublishReceipt(string toEmail, string bookingType, string summary, decimal amount)
        {
            var factory = new ConnectionFactory(_brokerUri);
            using var conn    = factory.CreateConnection();
            using var session = conn.CreateSession(AcknowledgementMode.AutoAcknowledge);
            var dest          = session.GetQueue("receipt.queue");
            using var producer = session.CreateProducer(dest);
            conn.Start();

            producer.Send(session.CreateTextMessage($"{toEmail}|{bookingType}|{summary}|{amount}"));
        }
    }
}
