using MSJServer.HTTP;
using System.Net;
using System.Text;

namespace MSJServer
{
    public partial class Session
    {
        public Guid SessionID { get;private set; } //unique session identifier
        public Account LoggedInAccount { get; private set; } //account associated with session
        public DateTime TimeOutTime { get; private set; } //time representing when the session will timeout

        public bool TimedOut => DateTime.Now > TimeOutTime;

        public Session(Account loggedInAccount)
        {
            LoggedInAccount = loggedInAccount;
            TimeOutTime = DateTime.Now.AddMinutes(15);
            SessionID = Guid.NewGuid();

            loggedInAccount.IsLoggedIn = true;
        }

        public void End() => LoggedInAccount.IsLoggedIn = false;
    }

    partial class Server : HTTPServer
    {
        private List<Session> sessionsToEnd = new(10);

        public Server(AccountValidator accountValidator)
        {
            accounts = LoadAccounts(accountValidator);

            POST["/login"] = HandleLogin;
            POST["/signup"] = HandleSignup;
            GET["/logout"] = HandleLogout;
            GET["/verify_landing"] = HandleVerifyLanding;
            GET["/validate_code"] = HandleValidateVerificationCode;
            GET["/userinfo"] = HandleGetUserInfo;
            GET["/setperms"] = HandleSetUserPermission;
            GET["/article"] = HandleReadArticle;
            POST["/upload"] = HandleUploadArticle;
            POST["/revise"] = HandleReviseArticle;
            GET["/revise_edit"] = HandleRevisionEditor;
            GET["/editor"] = HandleEditorRequest;
            POST["/comment"] = HandleCommentRequest;
            GET["/index"] = HandleFrontPageAccess;
            GET["/resolve_notif"] = HandleResolveNotification;
            GET["/logs"] = HandleFetchLogs;
            ServeStatic(new DirectoryInfo("static"));
            FinalizeConstructor();

            base.initialized = true;
            ThreadPool.QueueUserWorkItem((o) =>
            {
                Logger.Log(Logger.Severity.Information, "Server has started listening.");

                while (true)
                {
                    lock (sessions) //lock (sessionsToEnd)
                    {
                        foreach (Session session in sessions.Values)
                        {
                            if (session.TimedOut)
                                sessionsToEnd.Add(session);
                        }

                        foreach(Session session in sessionsToEnd)
                        {
                            sessions.Remove(session.SessionID);

                            lock (activeVerificationCodes)
                            {
                                if (activeVerificationCodes.ContainsKey(session.LoggedInAccount))
                                    activeVerificationCodes.Remove(session.LoggedInAccount);
                            }

                            session.End();
                        }

                        sessionsToEnd.Clear();
                    };
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
