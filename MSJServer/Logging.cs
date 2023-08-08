using MSJServer.HTTP;
using System.Diagnostics;
using System.Net;
using System.Text;

namespace MSJServer
{
    public static class Logger
    {
        public enum Severity : byte
        {
            Critical = 4,
            Alert = 3,
            Warning = 2,
            Information = 1
        }

        public struct Event
        {
            private static IPAddress? ReadIPAddress(BinaryReader reader)
            {
                int length = reader.ReadByte();
                if (length <= 0)
                    return null;
                return new IPAddress(reader.ReadBytes(length));
            }

            public Severity Severity { get; private set; }
            public string Description { get; private set; }
            
            public string? Username { get; private set; }
            public IPAddress? Address { get; private set; }
            public DateTime Time { get; private set; }

            public Event(Severity severity, string description, string? username, IPAddress? address, DateTime time)
            {
                Severity = severity;
                Description = description;
                Username = username;
                Address = address;
                Time = time;
            }

            public Event(BinaryReader reader) : this((Severity)reader.ReadByte(), reader.ReadString(), reader.ReadBoolean() ? reader.ReadString() : null, ReadIPAddress(reader), new DateTime(reader.ReadInt64()))
            {

            }

            public void WriteTo(BinaryWriter writer)
            {
                writer.Write((byte)Severity);
                writer.Write(Description);

                if (Username == null)
                    writer.Write(false);
                else
                {
                    writer.Write(true);
                    writer.Write(Username);
                }
                if(Address == null)
                    writer.Write((byte)0);
                else
                {
                    byte[] addressBytes = Address.GetAddressBytes();
                    Debug.Assert(addressBytes.Length <= byte.MaxValue);

                    writer.Write((byte)addressBytes.Length);
                    writer.Write(addressBytes);
                }

                writer.Write(Time.Ticks);
            }
        }
        
        private static object lockObject = new object();

        static Logger()
        {
            if (!Directory.Exists("logs"))
                Directory.CreateDirectory("logs");
        }

        public static void Log(Severity severity, string description, string? username = null, IPAddress? address = null)
        {
            lock (lockObject)
            {
                using (FileStream stream = new FileStream(Path.Combine("logs", $"log_{DateTime.Now.Date.ToString("yyyy-dd-M")}"), FileMode.Append, FileAccess.Write, FileShare.None))
                using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8))
                {
                    Event @event = new Event(severity, description, username, address, DateTime.Now);
                    @event.WriteTo(writer);
                }
            }
        }

        public static Event[] LoadEvents(DateTime from, DateTime to, int offset = 0, int limit = 10, string? filterUser = null, IPAddress? filterIp = null, Severity minSeverity = Severity.Information)
        {
            List<Event> events = new(Math.Min(100,limit));

            DateTime start = from;
            TimeSpan timeSpan = to - from;
            
            int scanned = 0;
            for(int i = (int)timeSpan.TotalDays; i >= 0; i--)
            {
                string fileName = Path.Combine("logs", $"log_{start.AddDays(i).ToString("yyyy-dd-M")}");
                if (!File.Exists(fileName))
                    continue;

                lock (lockObject)
                {
                    using (FileStream stream = new FileStream(fileName, FileMode.Open, FileAccess.Read))
                    using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8))
                    {
                        while (stream.Position < stream.Length)
                        {
                            Event @event = new Event(reader);
                            if (@event.Time < from)
                                continue;
                            if (@event.Time > to)
                                break;
                            if (filterUser != null && filterUser != @event.Username)
                                continue;
                            if (filterIp != null && filterIp != @event.Address)
                                continue;
                            if (@event.Severity < minSeverity)
                                continue;

                            if (scanned >= offset)
                            {
                                events.Add(@event);

                                if (events.Count == limit)
                                    return events.ToArray();
                            }

                            scanned++;
                        }
                    }
                }
            }
            return events.ToArray();
        }
    }

    partial class Server
    {
        private void HandleFetchLogs(HttpListenerContext context)
        {
            Account? account = GetLoggedInAccount(context);
            if(account == null)
            {
                RespondError(context, "You must log in to access logs.");
                return;
            }

            Dictionary<string, string> rangeInfo = context.Request.GetGETData();

            DateTime to = rangeInfo.ContainsKey("to") ? DateTime.Now.AddDays(-double.Parse(rangeInfo["to"])) : DateTime.Now;
            DateTime from = rangeInfo.ContainsKey("span") ? to.AddDays(-double.Parse(rangeInfo["span"])) : to.AddDays(-7);

            int page = rangeInfo.ContainsKey("page") ? int.Parse(rangeInfo["page"]) : 0;
            int pageSize = rangeInfo.ContainsKey("pagesize") ? int.Parse(rangeInfo["pagesize"]) : 10;

            string? filterUser = rangeInfo.ContainsKey("user") ? rangeInfo["user"] : null;
            IPAddress? filterIp = rangeInfo.ContainsKey("addr") ? IPAddress.Parse(rangeInfo["addr"]) : null;

            if(account.Permissions < Permissions.Admin && (filterUser == null || filterUser != account.Name))
            {
                context.Request.Log(Logger.Severity.Alert, $"Unauthorized attempt to access logs.", account.Name);
                RespondError(context, "You must be an admin to access another users logs.");
                return;
            }

            StringBuilder stringBuilder = new StringBuilder();
            
            stringBuilder.Append("<table><tr><th>Severity</th><th>Timestamp</th><th>Description</th>");
            if (filterUser == null)
                stringBuilder.Append("<th>Username</th>");
            if (filterIp == null)
                stringBuilder.Append("<th>IP Address</th>");
            stringBuilder.Append("</tr>");
            foreach(Logger.Event @event in Logger.LoadEvents(from, to, page * pageSize, pageSize, filterUser, filterIp))
            {
                stringBuilder.Append($"<tr><td>{@event.Severity}</td><td>{@event.Time.ToString()}</td><td>{@event.Description}</td>");
                if (filterUser == null)
                    stringBuilder.Append(@event.Username == null ? "<td>N/A</td>" : $"<td>{@event.Username}</td>");
                if (filterIp == null)
                    stringBuilder.Append(@event.Address == null ? "<td>N/A</td>" : $"<td>{@event.Address}</td>");
                stringBuilder.Append("</tr>");
            }

            Respond202(context, stringBuilder.ToString());
        }
    }
}