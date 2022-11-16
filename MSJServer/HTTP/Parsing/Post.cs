using System.Net;
using System.Text;
using System.Web;

namespace MSJServer.HTTP
{
    public static partial class HttpListenerRequestExtensions
    {
        public static Dictionary<string, string> GetPOSTData(this HttpListenerRequest request)
        {
            Dictionary<string, string> keyValuePairs = new Dictionary<string, string>();
            using (StreamReader reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                while (!reader.EndOfStream)
                {
                    KeyValuePair<string, string> keyValuePair = ReadKeyPair(reader);
                    keyValuePairs.Add(keyValuePair.Key, keyValuePair.Value);
                }
            }
            return keyValuePairs;
        }

        public static Dictionary<string, string> GetGETData(this HttpListenerRequest request)
        {
            Dictionary<string, string> keyValuePairs = new Dictionary<string, string>(request.QueryString.Count);
            foreach(string? key in request.QueryString.Keys)
                if (key != null)
#pragma warning disable CS8604 // Possible null reference argument.
                    keyValuePairs.Add(key, HttpUtility.UrlDecode(request.QueryString.Get(key), Encoding.UTF8));
#pragma warning restore CS8604 // Possible null reference argument.
            return keyValuePairs;
        }

        private static KeyValuePair<string, string> ReadKeyPair(StreamReader reader)
        {
            string key = "";
            char last_char;
            while (!reader.EndOfStream && (last_char = (char)reader.Read()) != '=')
            {
                key += last_char;
            }
            string value = "";
            while (!reader.EndOfStream && (last_char = (char)reader.Read()) != '&')
            {
                if (last_char == '+')
                    value += ' ';
                else
                    value += last_char;
            }
            return new KeyValuePair<string, string>(key, HttpUtility.UrlDecode(value, Encoding.UTF8));
        }
    }
}
