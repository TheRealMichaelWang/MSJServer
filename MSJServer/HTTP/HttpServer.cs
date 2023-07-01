using System.Net;
using System.Text;

namespace MSJServer.HTTP
{
    public partial class HTTPServer
    {
        public string Hostname => "*";

        public string Scheme => "http";

        public string BaseURL => BuildUri();

        private readonly RequestRouter router;
        private readonly HttpListener listener;
        protected volatile bool initialized = false;

        public HTTPServer()
        {
            if (!HttpListener.IsSupported)
                throw new NotSupportedException("Needs Windows XP SP2, Server 2003, or later");
            router = new RequestRouter();
            listener = new HttpListener();
        }

        protected void FinalizeConstructor()
        {
            router.GetAllRoutes().ToList().ForEach(route =>
            {
                string query = string.Empty;
                if (route.Contains('?'))
                {
                    query = route.Substring(route.IndexOf('?') + 1);
                    route = route.Substring(0, route.IndexOf('?'));
                }
                if (!route.EndsWith("/"))
                    route += "/";
                listener.Prefixes.Add(BuildUri(route, query));
            });
            listener.Prefixes.Add(BuildUri());
        }

        public void ServeStatic(DirectoryInfo directory, string path="") => router.ServeStatic(directory, path);

        private string BuildUri(string path = "", string query = "") => new UriBuilder(Scheme, Hostname, 80, path, query).ToString();

        public void Start()
        {
            while (!initialized) { }

            listener.Start();
            ThreadPool.QueueUserWorkItem((o) =>
            {
                try
                {
                    while (listener.IsListening)
                    {
                        ThreadPool.QueueUserWorkItem((c) =>
                        {
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
                            HttpListenerContext context = c as HttpListenerContext;
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
                            try
                            {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                                Action<HttpListenerContext>? handler = router.FindRegister(context.Request, this);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                                if (handler == null)
                                    Respond404(context);
                                else
                                    ProcessRequest(context, handler);
                            }
                            finally
                            {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                                context.Response.OutputStream.Close();
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                            }
                        }, listener.GetContext());
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            });
        }

        public void Stop()
        {
            listener.Stop();
            listener.Close();
        }

        private void ProcessRequest(HttpListenerContext context, Action<HttpListenerContext> handler)
        {
            try
            {
                handler(context);
            }
            catch
            {
                Respond500(context);
            }
        }

        public void Respond202(HttpListenerContext context, string content)
        {
            context.Response.StatusCode = 200;
            context.Response.StatusDescription = "Your request has been succesfully fullfilled.";
            byte[] buffer = Encoding.UTF8.GetBytes(content);
            context.Response.ContentLength64 = buffer.Length;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
        }

        public void Respond404(HttpListenerContext context)
        {
            context.Response.StatusCode = 404;
            context.Response.StatusDescription = "The server could not route anything to the given URI.";
        }

        public void Respond500(HttpListenerContext context)
        {
            context.Response.StatusCode = 500;
            context.Response.StatusDescription = "Due to an unexpected error, the server was unabble to fullfill your request";
        }

        public void Redirect(HttpListenerContext context, string newpage)
        {
            context.Response.Redirect(newpage);
            //response.StatusCode = 303;
            //response.StatusDescription = "Redirecting...";
            //byte[] buffer = Encoding.UTF8.GetBytes(newpage);
            //response.ContentLength64 = buffer.Length;
            //response.OutputStream.Write(buffer, 0, buffer.Length);
        }

        #region IDisposable Support

        private bool disposed = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    if (listener.IsListening)
                    {
                        Stop();
                    }
                }
                disposed = true;
            }
        }

        public void Dispose() => Dispose(true);
        #endregion
    }
}
