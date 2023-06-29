using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MSJServer.HTTP
{
    class RequestRouter
    {
        private readonly Dictionary<HttpMethod, RequestHandlerRegister> requestHandlerRegisters;
        private readonly Dictionary<string, DirectoryInfo> servedStatic;

        public RequestRouter()
        {
            requestHandlerRegisters = new Dictionary<HttpMethod, RequestHandlerRegister>();
            servedStatic = new Dictionary<string, DirectoryInfo>();
        }

        /*
         * Gets registrator for http method
         */
        public RequestHandlerRegister GetRequestHandlerRegister(HttpMethod httpMethod)
        {
            if (!requestHandlerRegisters.ContainsKey(httpMethod))
            {
                requestHandlerRegisters.Add(httpMethod, new RequestHandlerRegister(httpMethod));
            }
            return requestHandlerRegisters[httpMethod];
        }

        /*
         * Returns all possible http routes
         */
        public IEnumerable<string> GetAllRoutes() => requestHandlerRegisters.Values.SelectMany(r => r.Handlers.Keys).Union(servedStatic.Keys);

        /*
         * Gets a request handler from an http handler. Can return volatile data.
         */
        public Action<HttpListenerContext>? FindRegister(HttpListenerRequest httpListenerRequest, HTTPServer server)
        {
            HttpMethod? requestMethod = httpListenerRequest.GetHttpMethod();
            if (!requestMethod.HasValue)
                return null;

            if (requestHandlerRegisters.ContainsKey(requestMethod.Value))
            {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
#pragma warning disable CS8604 // Possible null reference argument.
                if (requestHandlerRegisters[requestMethod.Value].Handlers.ContainsKey(httpListenerRequest.Url.AbsoluteUri))
                    return requestHandlerRegisters[requestMethod.Value].Handlers[httpListenerRequest.Url.AbsoluteUri];
                else if (requestHandlerRegisters[requestMethod.Value].Handlers.ContainsKey(httpListenerRequest.RawUrl))
                    return requestHandlerRegisters[requestMethod.Value].Handlers[httpListenerRequest.RawUrl];
                else if (requestHandlerRegisters[requestMethod.Value].Handlers.ContainsKey(httpListenerRequest.Url.LocalPath))
                    return requestHandlerRegisters[requestMethod.Value].Handlers[httpListenerRequest.Url.LocalPath];
#pragma warning restore CS8604 // Possible null reference argument.
#pragma warning restore CS8602 // Dereference of a possibly null reference.
            }
            if (requestMethod == HttpMethod.Get)
            {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                string? staticMatch = servedStatic.Keys.FirstOrDefault(k => httpListenerRequest.Url.AbsolutePath.StartsWith(k));
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                string fileRelPath;
                if (staticMatch != null)
                {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    fileRelPath = httpListenerRequest.Url.AbsolutePath.Substring(staticMatch.Length);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                    if (fileRelPath == "" || fileRelPath == "index")
                        return requestHandlerRegisters[HttpMethod.Get].Handlers["/index"];
                    string absoloute_path = Path.Combine(servedStatic[staticMatch].FullName, fileRelPath).Replace("\\", "/");
                    if (!absoloute_path.StartsWith("/") && !absoloute_path.StartsWith("C:"))
                        absoloute_path = '/' + absoloute_path;
                    if (File.Exists(absoloute_path))
                        return (context) => server.Respond202(context, File.ReadAllText(absoloute_path));
                }
            }
            return null;
        }

        /*
         *Adds a static request handler
         */
        public void ServeStatic(DirectoryInfo directory, string path = "")
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            if (servedStatic.ContainsKey(path))
                throw new ArgumentException("Directory is already served statically at path " + path + ".", path);
            path = path.Trim();
            if (string.IsNullOrWhiteSpace(path))
                path = "/";
            else
            {
                if (!path.StartsWith("/"))
                    path = '/' + path;
                if (!path.EndsWith("/"))
                    path += '/';
            }

            servedStatic[path] = directory;
        }
    }
}
