using MSJServer.HTTP;
using System.Net;
using System.Text;

namespace MSJServer
{
    partial class Server : HTTPServer
    {
        public Server()
        {
            accounts = LoadAccounts();

            POST["/login"] = HandleLogin;
            POST["/signup"] = HandleSignup;
            GET["/userinfo"] = HandleGetUserInfo;
            GET["/setperms"] = HandleSetUserPermission;
            GET["/article"] = HandleReadArticle;
            POST["/upload"] = HandleUploadArticle;
            POST["/revise"] = HandleReviseArticle;
            GET["/revise_edit"] = HandleRevisionEditor;
            GET["/editor"] = HandleEditorRequest;
            POST["/comment"] = HandleCommentRequest;
            GET["/index"] = HandleFrontPageAccess;
            ServeStatic(new DirectoryInfo("static"));

            base.initialized = true;
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

        public void RespondError(HttpListenerContext context, string errorTitle, params string[] errorReasons)
        {
            string content = File.ReadAllText("templates/error.html");
            content = content.Replace("{ERRORTITLE}", errorTitle);
            if (errorReasons.Length == 0)
                content = content.Replace("{ERRORDETAILS}", "No further information availible.");
            else if(errorReasons.Length == 1)
                content = content.Replace("{ERRORDETAILS}", errorReasons[0]);
            else
            {
                StringBuilder builder = new();
                builder.Append("One or more of the following reasons:<br>");
                builder.Append("<ol type=\"1\">");
                foreach (string reason in errorReasons)
                    builder.Append($"<li>{reason}</li>");
                builder.Append("</ol>");
                content = content.Replace("{ERRORDETAILS}", builder.ToString());
            }
            Respond202(context, content);
        }
    }
}
