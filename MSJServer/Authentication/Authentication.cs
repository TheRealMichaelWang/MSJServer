using MSJServer.HTTP;
using System.Net;
using System.Text.RegularExpressions;

namespace MSJServer
{
    partial class Session
    {
        public void ExtendSession() => TimeOutTime = DateTime.Now.AddMinutes(15);
    }

    partial class Server
    {
        private Dictionary<Guid, Session> sessions = new();

        private Session? GetCurrentSession(HttpListenerContext context)
        {
            Cookie? session = context.Request.Cookies["session"];
            if (session == null)
                return null;
            Guid id = Guid.Parse(session.Value);

            lock (sessions)
            {
                if (!sessions.ContainsKey(id))
                    return null;

                sessions[id].ExtendSession();
                return sessions[id];
            }
        }

        private Account? GetLoggedInAccount(HttpListenerContext context) => GetCurrentSession(context)?.LoggedInAccount;

        private Session? LogAccountIn(HttpListenerContext context, Account account)
        {
            if (account.IsLoggedIn == true)
            {
                RespondError(context, "Failed to Login", $"You've already logged in, {account.Name}.");
                return null;
            }

            Session session = new Session(account);
            lock (sessions)
            {
                sessions.Add(session.SessionID, session);
            }

            context.Response.SetCookie(new Cookie("session", session.SessionID.ToString()));

            Redirect(context, "index");

            return session;
        }

        private void HandleLogin(HttpListenerContext context)
        {
            if (GetLoggedInAccount(context) != null)
            {
                RespondError(context, $"You've already logged in. Log out before logging in with an alt-account.");
                return;
            }

            Dictionary<string, string> loginInfo = context.Request.GetPOSTData();

            if (!accounts.ContainsKey(loginInfo["username"]))
            {
                context.Request.Log(Logger.Severity.Alert, "Failed login attempt, invalid username.");
                RespondError(context, "Failed to Login", $"Username or email {loginInfo["username"]} doesn't exist.");
                return;
            }
            Account loggedOn = accounts[loginInfo["username"]];
            if (loggedOn.Password != loginInfo["password"])
            {
                context.Request.Log(Logger.Severity.Alert, "Failed login attempt, invalid password.", loggedOn.Email);
                RespondError(context, "Failed to Login", $"Wrong password recieved.");
                return;
            }

            if (!loggedOn.IsVerified)
            {
                Notification.MakeNotification(loggedOn, "You still haven't verified your account!", $"Seriously, if you don't verify your account (and identity) by {loggedOn.CreationDate.AddDays(7).ToShortDateString()}, we'll delete your account!", Notification.Serverity.MustResolve, ("Verify My Account", "verify_landing"));
            }

            context.Request.Log(Logger.Severity.Information, $"User logged in (verified: {loggedOn.IsVerified}).", loggedOn.Name);
            LogAccountIn(context, loggedOn);
        }

        private void HandleLogout(HttpListenerContext context)
        {
            Session? session = GetCurrentSession(context);

            if(session == null)
            {
                RespondError(context, "Failed to log out.", "You must log in in order to log out.");
                return;
            }

            context.Request.Log(Logger.Severity.Information, "User logged out.", session.LoggedInAccount.Name);

            lock (sessionsToEnd)
            {
                sessionsToEnd.Add(session);
            }
            Redirect(context, "index");
        }

        private void HandleSignup(HttpListenerContext context)
        {
            if (GetLoggedInAccount(context) != null)
            {
                RespondError(context, $"You've already logged in. Log out before making an alt-account.");
                return;
            }

            Dictionary<string, string> signupInfo = context.Request.GetPOSTData();

            if (!Regex.IsMatch(signupInfo["username"], "[a-zA-Z0-9]{8,25}"))
            {
                context.Request.Log(Logger.Severity.Warning, "Failed to signup, username is invalid.");
                RespondError(context, "Failed to Register New Account", "We apologize, but usernames must be alphanumerical and between 8 and 25 characters.");
                return;
            }
            if (!Regex.IsMatch(signupInfo["email"], "[a-z]+[0-9]{4}@mymail\\.lausd\\.net"))
            {
                context.Request.Log(Logger.Severity.Warning, "Failed to signup, email is invalid.");
                RespondError(context, "Failed to Register New Account", "We apologize, but we're only accepting school emails([a-z]+[0-9]{4}@mymail\\.lausd\\.net) at the moment.");
                return;
            }

            Account? newAccount = RegisterAccount(signupInfo["username"], signupInfo["password"], signupInfo["email"]);
            if(newAccount == null)
            {
                context.Request.Log(Logger.Severity.Alert, "Failed to signup, credentails in use.");
                RespondError(context, "Failed to Register New Account", $"Username {signupInfo["username"]} has already taken or email {signupInfo["email"]} is already in use.", "If you beleive your email to be in use without your authorization, please contact an admin, or wait 7 days for the offending account to be automatically removed.");
                return;
            }

            Notification.MakeNotification(newAccount, "Welcome to the MSJ!", "We're glad you're here to join us. Verify your account as soon as possible within the next 7 days(or else...)!", Notification.Serverity.ShouldResolve, ("Verify My Account", "verify_landing"), false);
            LogAccountIn(context, newAccount);
            context.Request.Log(Logger.Severity.Information, "User signed up.", newAccount.Name);
        }

        private void HandleGetUserInfo(HttpListenerContext context)
        {
            Dictionary<string, string> signupInfo = context.Request.GetGETData(); // context.Request.GetPOSTData();

            if (!accounts.ContainsKey(signupInfo["username"]))
            {
                context.Request.Log(Logger.Severity.Warning, $"Failed to request user info, invalid username {signupInfo["username"]}");
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

            context.Request.Log(Logger.Severity.Information, $"User ({account.Name}) information requested.", loggedInAccount?.Name);
        }

        private void HandleSetUserPermission(HttpListenerContext context)
        {
            Dictionary<string, string> permissionsInfo = context.Request.GetGETData();

            Account? loggedInAccount = GetLoggedInAccount(context);
            if(loggedInAccount == null || loggedInAccount.Permissions != Permissions.Admin)
            {
                context.Request.Log(Logger.Severity.Alert, "Failed to set permissions, insufficient permissions.", loggedInAccount?.Name);
                RespondError(context, "Couldn't Set User Permissions", "You must be logged in as an administrator to change user permissions.");
                return;
            }

            if (!accounts.ContainsKey(permissionsInfo["username"]))
            {
                context.Request.Log(Logger.Severity.Warning, "Failed to set permissions, invalid username.");
                RespondError(context, "Couldn't Set User Permissions", $"User {permissionsInfo["username"]} doesn't exist.");
                return;
            }
            Account account = accounts[permissionsInfo["username"]];
            
            if(loggedInAccount == account)
            {
                context.Request.Log(Logger.Severity.Warning, "Failed to set permissions, conflict of interest.", account.Name);
                RespondError(context, "Couldn't Set User Permissions", "Potential conflict of interest; you cannot change your own permissions.");
                return;
            }

            if(!SetAccountPerms(account, permissionsInfo["perms"]))
            {
                context.Request.Log(Logger.Severity.Warning, "Failed to set permissions, invalid permissions.", loggedInAccount.Name);
                RespondError(context, "Couldn't Set User Permissions", $"Unrecognized Permission Level {permissionsInfo["perms"]}.");
                return;
            }

            Notification.MakeNotification(account, "Your MSJ Permissions have been Changed", $"Your permissions have been set to {PermissionsHelper.GetDescription(account.Permissions)} by {loggedInAccount.Name}.", Notification.Serverity.CanIgnore);
            Redirect(context, $"/userinfo?username={account.Name}");
            context.Request.Log(Logger.Severity.Information, $"{account.Name}'s permissions set to {permissionsInfo["perms"]}.", loggedInAccount.Name);
        }
    }
}
