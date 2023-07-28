using System.Net;

namespace MSJServer.HTTP
{
    public static partial class HttpListenerRequestExtensions
    {
        public static HttpMethod? GetHttpMethod(this HttpListenerRequest request)
        {
            try
            {
                return (HttpMethod)Enum.Parse(typeof(HttpMethod), request.HttpMethod, true);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        public static void Log(this HttpListenerRequest request, Logger.Severity severity, string message, string? user = null)
        {
            Logger.Log(severity, message, user, request.RemoteEndPoint.Address);
        }

        
    }
}
