using MSJServer.HTTP;
using DnsClient;
using DnsClient.Protocol;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;

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

    public class Notification
    {
        private const int BodyCharacterLimit = 115;

        public enum Serverity : byte
        {
            MustResolve = 2,
            ShouldResolve = 1,
            CanIgnore = 0
        }

        private static string EnsureNotificationsDir(Account account)
        {
            string notificationDir = Path.Combine("users", account.Name, "notifs");
            if (!Directory.Exists(notificationDir))
                Directory.CreateDirectory(notificationDir);
            return notificationDir;
        }

        public static Notification[] GetNotifications(Account account, bool excludeRead)
        {
            string[] files = Directory.GetFiles(EnsureNotificationsDir(account));

            List<Notification> result = new List<Notification>(files.Length);

            for (int i = 0; i < files.Length; i++)
            {
                using (FileStream fileStream = new FileStream(files[i], FileMode.Open, FileAccess.Read))
                using (BinaryReader reader = new BinaryReader(fileStream, Encoding.UTF8))
                {
                    Notification notification = new Notification(reader, Guid.Parse(Path.GetFileName(files[i])), account);

                    if (!(excludeRead && notification.Read))
                        result.Add(notification);
                }
            }

            return result.ToArray();
        }

        public static Notification? FromId(Account account, Guid id)
        {
            string filePath = Path.Combine("users", account.Name, "notifs", id.ToString());
            if (!File.Exists(filePath))
                return null;

            using(FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            using(BinaryReader reader = new BinaryReader(fileStream, Encoding.UTF8))
            {
                return new Notification(reader, id, account);
            }
        }

        public static void MakeNotification(Account receiver, string subject, string body, Serverity serverity, (string, string)? resolveAction = null, bool deleteOnResolve=true)
        {
            EnsureNotificationsDir(receiver);

            Notification notification = new Notification(DateTime.Now, subject, body, resolveAction, false, serverity, deleteOnResolve, Guid.NewGuid(), receiver);
            notification.Save();

            if (receiver.IsVerified)
                EmailNotifier.Notify(receiver.Email, subject, body);
        }

        public static void MakeNotificationFromTemplate(Account receiver, string templateFile, params (string, string)[] textParams)
        {
            string src = File.ReadAllText($"templates/{templateFile}");
            src = src.Replace("{RECV}", receiver.Name);
            foreach ((string, string) textParam in textParams)
                src = src.Replace($"{{{textParam.Item1}}}", textParam.Item2);

            using(StringReader reader = new StringReader(src))
            {
                Dictionary<string, string> templateInfo = new Dictionary<string, string>();

                string? line;
                while((line = reader.ReadLine()) != null && (line.Contains(':') && line != string.Empty))
                {
                    if (line == string.Empty)
                        continue;

                    string[] parts = line.Split(':', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length != 2)
                        throw new FormatException($"In template file {templateFile}, line {line} is invalid.");

                    templateInfo.Add(parts[0].Trim(), parts[1].Trim());
                }

                try
                {
                    MakeNotification(receiver, templateInfo["subject"], reader.ReadToEnd(), (Serverity)byte.Parse(templateInfo["warning"]), templateInfo.ContainsKey("resolve") ? (templateInfo.ContainsKey("resolve-title") ? templateInfo["resolve-title"] : "Resolve", templateInfo["resolve"]) : null, templateInfo.ContainsKey("delete") ? bool.Parse(templateInfo["delete"]) : true);
                }
                catch (KeyNotFoundException e)
                {
                    throw new FormatException($"In template file {templateFile}, a mandatory parameter is missing ({e.Message}).");
                }
            }
        }

        public DateTime Time { get; private set; }
        public string Subject { get; private set; }
        public string Body { get; private set; }
        public (string, string)? ResolveAction { get; private set; }
        public Serverity ResponseServerity { get; private set; }
        public bool DeleteOnResolve { get; private set; }

        public Account Receiver { get; private set; }
        public Guid Id { get; private set; }

        private bool deleted = false;
        private bool _read;
        private bool _resolved;
        private string FilePath => Path.Combine("users", Receiver.Name, "notifs", Id.ToString());

        public bool Resolved { get => _resolved; set { _read = true; _resolved = value; if (DeleteOnResolve && _resolved) Delete(); else Save(); } }
        public bool Read { get => _read; set { _read = value; if (_read && ResolveAction == null) Resolved = true; else Save(); } }

        private Notification(DateTime time, string subject, string body, (string, string)? resolveAction, bool read, Serverity responseSeverity, bool deleteOnResolve, Guid id, Account reciever)
        {
            Time = time;
            Subject = subject;
            Body = body;
            ResolveAction = resolveAction;
            _read = read;
            ResponseServerity = responseSeverity;
            DeleteOnResolve = deleteOnResolve;
            Id = id;
            Receiver = reciever;
        }

        private Notification(BinaryReader reader, Guid id, Account reciever) : this(new DateTime(reader.ReadInt64()), reader.ReadString(), reader.ReadString(), reader.ReadBoolean() ? (reader.ReadString(), reader.ReadString()) : null, reader.ReadBoolean(), (Serverity)reader.ReadByte(), reader.ReadBoolean(), id, reciever)
        {

        }

        public void Save()
        {
            using (FileStream fileStream = new FileStream(FilePath, FileMode.OpenOrCreate, FileAccess.Write))
            using (BinaryWriter writer = new BinaryWriter(fileStream, Encoding.UTF8))
            {
                Write(writer);
            }
        }

        private void Write(BinaryWriter writer)
        {
            writer.Write(Time.Ticks);
            writer.Write(Subject);
            writer.Write(Body);
            if (ResolveAction == null)
                writer.Write(false);
            else
            {
                writer.Write(true);
                writer.Write(ResolveAction.Value.Item1);
                writer.Write(ResolveAction.Value.Item2);
            }
            writer.Write(Read);
            writer.Write((byte)ResponseServerity);
            writer.Write(DeleteOnResolve);
        }

        public void Delete()
        {
            if (deleted)
                return;

            deleted = true;
            File.Delete(FilePath);
        }

        public string ToHTML()
        {
            StringBuilder htmlBuilder = new();

            htmlBuilder.Append("<div class=\"");

            htmlBuilder.Append(ResponseServerity switch
            {
                Serverity.MustResolve => "alert alert-danger",
                Serverity.ShouldResolve => "alert alert-warning",
                Serverity.CanIgnore => "alert alert-secondary",
                _ => throw new InvalidOperationException()
            });

            htmlBuilder.Append("\"><b>");
            htmlBuilder.Append(Subject);
            htmlBuilder.Append("</b><br>");

            if (Body.Length > BodyCharacterLimit)
                htmlBuilder.Append(Body.Substring(0, BodyCharacterLimit).Replace("\r\n", "<br>").Replace("\n", "<br>") + "...");
            else
                htmlBuilder.Append(Body.Replace("\r\n", "<br>").Replace("\n", "<br>"));

            if(ResolveAction != null)
            {
                htmlBuilder.Append("<br><a class=\"btn btn-primary btn-sm\" href=\"/resolve_notif?notifid=");
                htmlBuilder.Append(Id.ToString());
                htmlBuilder.Append("\">");
                htmlBuilder.Append(ResolveAction.Value.Item1);
                htmlBuilder.Append("</a>");
            }

            htmlBuilder.Append("</div>");
            return htmlBuilder.ToString();
        }
    }

    partial class Server
    {
        private void HandleResolveNotification(HttpListenerContext context)
        {
            Account? account = GetLoggedInAccount(context);
            if(account == null)
            {
                RespondError(context, "You must log in to respond to a notification.");
                return;
            }

            Dictionary<string, string> queryInfo = context.Request.GetGETData();

            Guid id;
            if (!Guid.TryParse(queryInfo["notifid"], out id))
            {
                RespondError(context, $"{queryInfo["notifid"]} is not a valid GUID.");
                return;
            }

            Notification? notification = Notification.FromId(account, id);
            if(notification == null)
            {
                RespondError(context, $"Unable to load notification {id}. It probably doesn't exist anymore, if it did at all.");
                return;
            }
            else if(notification.ResolveAction == null)
            {
                RespondError(context, $"No resolve action configured for notification {id}.");
                return;
            }

            Redirect(context, notification.ResolveAction.Value.Item2);
            notification.Resolved = true;

            return;
        }
    }
}