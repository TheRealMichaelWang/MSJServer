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
    }
}
