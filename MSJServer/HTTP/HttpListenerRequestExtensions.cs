using System.Net;

namespace MSJServer.HTTP
{
    public static partial class HttpListenerRequestExtensions
    {
        public static HttpMethod GetHttpMethod(this HttpListenerRequest request)
        {
            return (HttpMethod)Enum.Parse(typeof(HttpMethod), request.HttpMethod, true);
        }
    }
}
