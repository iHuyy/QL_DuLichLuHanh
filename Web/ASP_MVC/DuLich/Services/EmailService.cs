using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace DuLich.Services
{
    public class EmailService
    {
        private readonly IConfiguration _config;

        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            // Lấy cấu hình từ appsettings.json
            var host = _config["EmailSettings:Host"] ?? "smtp.gmail.com";
            var port = int.Parse(_config["EmailSettings:Port"] ?? "587");
            var mail = _config["EmailSettings:Mail"];
            var password = _config["EmailSettings:Password"];

            if (string.IsNullOrEmpty(mail) || string.IsNullOrEmpty(password))
            {
                throw new System.Exception("Chưa cấu hình Email/Password trong appsettings.json");
            }

            using var client = new SmtpClient(host, port)
            {
                // QUAN TRỌNG: Phải đặt UseDefaultCredentials = false TRƯỚC khi gán Credentials
                UseDefaultCredentials = false, 
                Credentials = new NetworkCredential(mail, password),
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(mail, "DuLich App Support"),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };
            
            mailMessage.To.Add(toEmail);

            await client.SendMailAsync(mailMessage);
        }
    }
}