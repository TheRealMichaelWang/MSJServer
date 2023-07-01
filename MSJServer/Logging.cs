using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

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

        static Logger()
        {
            if (!Directory.Exists("logs"))
                Directory.CreateDirectory("logs");
        }

        public static void Log(Severity severity, string description, string? username, IPAddress? address)
        {
            using(FileStream stream = new FileStream(Path.Combine("logs", $"log_{DateTime.Now.Date.ToString("yyyy-dd-M")}"), FileMode.Append, FileAccess.Write))
            using(BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                Event @event = new Event(severity, description, username, address, DateTime.Now);
                @event.WriteTo(writer);
            }
        }

        public static Event[] LoadEvents(DateTime from, DateTime to, int offset = 0, int limit = int.MaxValue)
        {
            List<Event> events = new(10);

            DateTime start = from.Date;
            TimeSpan timeSpan = to - from;
            
            int scanned = 0;
            for(int i = 0; i < timeSpan.TotalDays; i++)
            {
                string fileName = $"log_{start.ToString("yyyy-dd-M")}";
                if (!File.Exists(fileName))
                    break;

                using (FileStream stream = new FileStream(Path.Combine("logs", fileName), FileMode.Open, FileAccess.Read))
                using(BinaryReader reader = new BinaryReader(stream, Encoding.UTF8))
                {
                    while(stream.Position < stream.Length)
                    {
                        Event @event = new Event(reader);
                        if (@event.Time < from)
                            continue;
                        if (@event.Time > to)
                            break;

                        if(scanned < offset)
                        {
                            events.Add(@event);

                            if (events.Count == limit)
                                return events.ToArray();
                        }

                        scanned++;
                    }
                }

                start = start.AddDays(1);
            }
            return events.ToArray();
        }
    }
}