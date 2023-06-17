using DnsClient;
using DnsClient.Protocol;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace MSJServer
{
    public static class EmailNotifier
    {
        private static MxRecord SMTPServerRecord;

#pragma warning disable CS8618
        static EmailNotifier()
#pragma warning restore CS8618 
        {
            LookupClient client = new();
#pragma warning disable CS8600 // Almost always will return answers, gmail.com is gmail
#pragma warning disable CS8601
            SMTPServerRecord = (MxRecord)client.Query("gmail.com", QueryType.MX).Answers.MinBy((answer) => ((MxRecord)answer).Preference);
#pragma warning restore CS8601
#pragma warning restore CS8600
            ServicePointManager.ServerCertificateValidationCallback += new RemoteCertificateValidationCallback(ValidateRemoteCertificate);
        }

        private static bool ValidateRemoteCertificate(object sender, X509Certificate? cert, X509Chain? chain, SslPolicyErrors policyErrors) => policyErrors == SslPolicyErrors.RemoteCertificateNameMismatch && (cert != null && cert.Subject.EndsWith("google.com"));

        public static bool Notify(string email, string subject, string body)
        {
            using (SmtpClient client = new SmtpClient())
            using (MimeMessage message = new MimeMessage())
            {
                message.From.Add(InternetAddress.Parse("no-reply@themsj.org"));
                message.To.Add(InternetAddress.Parse(email));
                message.Subject = subject;
                message.Body = new TextPart(MimeKit.Text.TextFormat.Plain) { Text = body };

                try
                {
                    client.Connect(SMTPServerRecord.Exchange, 25, SecureSocketOptions.StartTls);

                    bool failed;
                    try
                    {
                        client.Send(message);
                        failed = false;
                    }
                    catch
                    {
                        failed = true;
                    }
                    finally
                    {
                        client.Disconnect(true);
                    }

                    return !failed;
                }
                catch
                {
                    return false; //connection was never established
                }
            }
        }
    }
}
