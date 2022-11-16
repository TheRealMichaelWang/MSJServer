using MSJServer.HTTP;
using System.Net;

namespace MSJServer
{
    partial class Server : HTTPServer
    {
        public Server()
        {
            POST["/login"] = HandleLogin;
            POST["/signup"] = HandleSignup;
            GET["/userinfo"] = HandleGetUserInfo;
            GET["/setperms"] = HandleSetUserPermission;
            GET["/article"] = HandleReadArticle;
            POST["/upload"] = HandleUploadArticle;
            GET["/editor"] = HandleEditorRequest;
            POST["/comment"] = HandleCommentRequest;
            GET["/index"] = HandleFrontPageAccess;

            ThreadPool.QueueUserWorkItem((o) =>
            {
                while (true)
                {
                    DateTime scanTime = DateTime.Now;
                    List<Guid> sessionsToRemove = new();
                    foreach (var session in sessions)
                    {
                        if (scanTime > session.Value.Item2)
                        {
                            sessionsToRemove.Add(session.Key);
                            session.Value.Item1.IsLoggedIn = false;
                        }
                    }
                    lock (sessions)
                    {
                        foreach (var session in sessionsToRemove)
                            sessions.Remove(session);
                    }
                }
            });
        }

        public void RespondError(HttpListenerContext context, string errorTitle, string errorDetails)
        {
            string content = File.ReadAllText("templates/error.html");
            content = content.Replace("{ERRORTITLE}", errorTitle);
            content = content.Replace("{ERRORDETAILS}", errorDetails);
            Respond202(context, content);
        }
    }
}
