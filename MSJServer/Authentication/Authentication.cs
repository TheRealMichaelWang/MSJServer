using MSJServer.HTTP;
using System.Net;
using System.Text.RegularExpressions;

namespace MSJServer
{
    partial class Server
    {
        private Dictionary<Guid, Tuple<Account, DateTime>> sessions = new();

        private Account? GetLoggedInAccount(HttpListenerContext context)
        {
            Cookie? session = context.Request.Cookies["session"];
            if (session == null)
                return null;
            Guid id = Guid.Parse(session.Value);

            lock (sessions)
            {
                if (!sessions.ContainsKey(id))
                    return null;
                sessions[id] = new(sessions[id].Item1, DateTime.Now.AddMinutes(15));
                return sessions[id].Item1;
            }
        }

        private Guid? LogAccountIn(HttpListenerContext context, Account account)
        {
            if (account.IsLoggedIn == true)
            {
                RespondError(context, "Failed to Login", $"You've already logged in, {account.Name}.");
                return null;
            }

            Guid sessionId = Guid.NewGuid();
            account.IsLoggedIn = true;
            Cookie session = new Cookie("session", sessionId.ToString());
            session.Expires = DateTime.Now.AddMinutes(15); //each session lasts for 16 minutes
            context.Response.SetCookie(session);

            lock (sessions)
            {
                sessions.Add(sessionId, new(account, session.Expires));
            }

            if (account.IsVerified)
                Redirect(context, "index");
            else
                Redirect(context, "verify_landing");

            return sessionId;
        }

        private void HandleLogin(HttpListenerContext context)
        {
            Dictionary<string, string> loginInfo = context.Request.GetPOSTData();

            if (!accounts.ContainsKey(loginInfo["username"]))
            {
                RespondError(context, "Failed to Login", $"Username or email {loginInfo["username"]} doesn't exist.");
                return;
            }
            Account loggedOn = accounts[loginInfo["username"]];
            if (loggedOn.Password != loginInfo["password"])
            {
                RespondError(context, "Failed to Login", $"Wrong password recieved.");
                return;
            }
            LogAccountIn(context, loggedOn);
        }

        private void HandleSignup(HttpListenerContext context)
        {
            Dictionary<string, string> signupInfo = context.Request.GetPOSTData();

            if (!Regex.IsMatch(signupInfo["username"], "[a-zA-Z0-9]{8,25}"))
            {
                RespondError(context, "Failed to Register New Account", "We apologize, but usernames must be alphanumerical and between 8 and 25 characters.");
                return;
            }
            if (!Regex.IsMatch(signupInfo["email"], "[a-z]+[0-9]{4}@mymail\\.lausd\\.net"))
            {
                RespondError(context, "Failed to Register New Account", "We apologize, but we're only accepting school emails([a-z]+[0-9]{4}@mymail\\.lausd\\.net) at the moment.");
                return;
            }

            Account? newAccount = RegisterAccount(signupInfo["username"], signupInfo["password"], signupInfo["email"]);
            if(newAccount == null)
            {
                RespondError(context, "Failed to Register New Account", $"Username {signupInfo["username"]} already taken.");
                return;
            }
            LogAccountIn(context, newAccount);
        }

        private void HandleGetUserInfo(HttpListenerContext context)
        {
            Dictionary<string, string> signupInfo = context.Request.GetGETData(); // context.Request.GetPOSTData();

            if (!accounts.ContainsKey(signupInfo["username"]))
            {
                RespondError(context, "Couldn't Fetch User Information", $"User {signupInfo["username"]} doesn't exist.");
                return;
            }
            Account account = accounts[signupInfo["username"]];

            string content;
            Account? loggedInAccount = GetLoggedInAccount(context);
            if (loggedInAccount != null && loggedInAccount.Permissions >= Permissions.Editor)
            {
                content = File.ReadAllText("templates/userinfo_admin.html");
                content = content.Replace("{EMAIL}", account.Email);
            }
            else
                content = File.ReadAllText("templates/userinfo.html");

            content = content.Replace("{USERNAME}", account.Name);
            content = content.Replace("{CREATIONDATE}", account.CreationDate.ToString());
            content = content.Replace("{PERMISSIONS}", PermissionsHelper.GetDescription(account.Permissions));
            content = content.Replace("{ISONLINE}", account.IsLoggedIn ? "Yes" : "No");
            content = content.Replace("{ISVERIF}", account.IsVerified ? "Yes" : "No");
            Respond202(context, content);
        }

        private void HandleSetUserPermission(HttpListenerContext context)
        {
            Dictionary<string, string> permissionsInfo = context.Request.GetGETData();

            if (!accounts.ContainsKey(permissionsInfo["username"]))
            {
                RespondError(context, "Couldn't Set User Permissions", $"User {permissionsInfo["username"]} doesn't exist.");
                return;
            }
            Account account = accounts[permissionsInfo["username"]];

            Account? loggedInAccount = GetLoggedInAccount(context);
            if(loggedInAccount != null && loggedInAccount.Permissions != Permissions.Admin)
            {
                RespondError(context, "Couldn't Set User Permissions", "You must be logged in as an administrator to change user permissions.");
                return;
            }
            else if(loggedInAccount == account)
            {
                RespondError(context, "Couldn't Set User Permissions", "Potential conflict of interest; you cannot change your own permissions.");
                return;
            }

            if(!SetAccountPerms(account, permissionsInfo["perms"]))
            {
                RespondError(context, "Couldn't Set User Permissions", $"Unrecognized Permission Level {permissionsInfo["perms"]}.");
                return;
            }
            Redirect(context, $"/userinfo?username={account.Name}");
        }
    }
}
