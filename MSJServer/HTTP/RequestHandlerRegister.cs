using System.Net;

namespace MSJServer.HTTP
{
    public class RequestHandlerRegister
    {
        public HttpMethod HttpMethod
        {
            get => httpMethod;
        }

        public Dictionary<string, Action<HttpListenerContext>> Handlers
        {
            get => handlers;
        }

        private readonly HttpMethod httpMethod;
        private readonly Dictionary<string, Action<HttpListenerContext>> handlers;

        public RequestHandlerRegister(HttpMethod httpMethod)
        {
            this.httpMethod = httpMethod;
            this.handlers = new Dictionary<string, Action<HttpListenerContext>>();
        }

        public Action<HttpListenerContext> this[string path]
        {
            get => handlers[path];
            set
            {
                handlers[path] = value;
            }
        }
    }
}
