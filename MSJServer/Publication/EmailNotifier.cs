using DnsClient;
using DnsClient.Protocol;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace MSJServer
{
    public static class EmailNotifier
    {
        private static MxRecord SMTPServer;

#pragma warning disable CS8618
        static EmailNotifier()
#pragma warning restore CS8618 
        {
            LookupClient client = new();
#pragma warning disable CS8600 // Almost always will return answers, gmail.com is gmail
#pragma warning disable CS8601
            SMTPServer = (MxRecord)client.Query("gmail.com", QueryType.MX).Answers.MinBy((answer) => ((MxRecord)answer).Preference);
#pragma warning restore CS8601
#pragma warning restore CS8600

            ServicePointManager.ServerCertificateValidationCallback += new RemoteCertificateValidationCallback(ValidateRemoteCertificate);
        }

        private static bool ValidateRemoteCertificate(object sender, X509Certificate? cert, X509Chain? chain, SslPolicyErrors policyErrors) => policyErrors == SslPolicyErrors.RemoteCertificateNameMismatch && (cert != null && cert.Subject.EndsWith("google.com"));

        public static void Notify(Account account, string subject, string body)
        {
            try
            {
                using (SmtpClient client = new(SMTPServer.Exchange, 25))
                using (MailMessage message = new MailMessage(new MailAddress("noreply@104.174.17.83", "The Magnolia Street Journal"), new MailAddress(account.Email, account.Name)))
                {
                    message.Subject = subject;
                    message.SubjectEncoding = Encoding.UTF8;
                    message.Body = body;
                    message.BodyEncoding = Encoding.UTF8;

                    client.UseDefaultCredentials = false;
                    client.EnableSsl = true;

                    client.Send(message);
                }
            }
            catch (SmtpException e)
            {
                ;
            }
        }
    }
}
