using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MSJServer.HTTP
{
    public enum HttpMethod
    {
        Get,
        Post,
        Put,
        Delete,
        Options,
        Connect,
        Patch,
        Head,
        Trace
    }

    public partial class HTTPServer
    {
        public RequestHandlerRegister GET
        {
            get => router.GetRequestHandlerRegister(HttpMethod.Get);
        }

        public RequestHandlerRegister POST
        {
            get => router.GetRequestHandlerRegister(HttpMethod.Post);
        }

        public RequestHandlerRegister PUT
        {
            get => router.GetRequestHandlerRegister(HttpMethod.Put);
        }

        public RequestHandlerRegister DELETE
        {
            get => router.GetRequestHandlerRegister(HttpMethod.Delete);
        }

        public RequestHandlerRegister OPTIONS
        {
            get => router.GetRequestHandlerRegister(HttpMethod.Options);
        }

        public RequestHandlerRegister PATCH
        {
            get => router.GetRequestHandlerRegister(HttpMethod.Patch);
        }

        public RequestHandlerRegister HEAD
        {
            get => router.GetRequestHandlerRegister(HttpMethod.Head);
        }

        public RequestHandlerRegister CONNECT
        {
            get => router.GetRequestHandlerRegister(HttpMethod.Connect);
        }

        public RequestHandlerRegister TRACE
        {
            get => router.GetRequestHandlerRegister(HttpMethod.Trace);
        }
    }
}
