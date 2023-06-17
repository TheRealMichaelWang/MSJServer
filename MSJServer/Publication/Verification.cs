using MSJServer.HTTP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MSJServer
{
    partial class Server
    {
        private Dictionary<Account, int> activeVerificationCodes = new();
        private Random verificationCodeGenerator = new();

        //checks taht the user is logged in
        //checks that an account is not already verified
        private Account? CheckAccount(HttpListenerContext context)
        {
            Account? account = GetLoggedInAccount(context);

            if (account == null)
            {
                RespondError(context, "Cannot Verify Account", "You must log in to get a verification code.");
                return null;
            }
            if (account.IsVerified)
            {
                RespondError(context, "Cannot Verify Account", "You've already verified your account.", "An admin has manually verified your account.");
                return null;
            }

            return account;
        }

        private void HandleVerifyLanding(HttpListenerContext context)
        {
            Account? account = CheckAccount(context);
            if (account == null)
                return;

            string content = File.ReadAllText("templates/verify_landing.html");
            content = content.Replace("{EMAIL}", account.Email);

            Dictionary<string, string> data = context.Request.GetGETData();
            if (!activeVerificationCodes.ContainsKey(account) || data.ContainsKey("resend"))
            {
                int verificationNumber = verificationCodeGenerator.Next(100000);
                if (!EmailNotifier.Notify(account.Email, "Verify your MSJ Account", $"Your verification number is \"{verificationNumber}\". Please verify your account as soon as possible, it will expire when your session ends."))
                {
                    RespondError(context, "Cannot Send Verification Code", "Failed to deliver verification email. Try contacting an admin to perform manual verification.");
                    return;
                }

                activeVerificationCodes[account] = verificationNumber;

                content = content.Replace("{BANNERMSG}", $"<div class=\"alert alert-secondary\">A verification code has just been sent to your email!</div>");
            }
            else if (data.ContainsKey("verif_code"))
                content = content.Replace("{BANNERMSG}", $"<div class=\"alert alert-danger\">The verification code provided, {data["verif_code"]} is invalid.</div>");
            else
                content = content.Replace("{BANNERMSG}", string.Empty);

            Respond202(context, content);
        }

        private void HandleValidateVerificationCode(HttpListenerContext context)
        {
            Account? account = CheckAccount(context);
            if (account == null)
                return;
            if (!activeVerificationCodes.ContainsKey(account))
            {
                Redirect(context, "verify_landing");
                return;
            }

            Dictionary<string, string> data = context.Request.GetGETData();
            if (!data.ContainsKey("verifcode"))
            {
                RespondError(context, "Cannot Verify Account", "GET Parameters missing \"verifcode\". Go to http://themsj.org/verify_landing to verify your account.");
                return;
            }

            int verificationCode;
            if (!int.TryParse(data["verifcode"], out verificationCode) || (verificationCode <= 0 || verificationCode >= 100000))
            {
                RespondError(context, "Cannot Verify Account", $"The verification code provided, {data["verifcode"]} is not a valid verification code. Verfication codes are 5 digit numericals.");
                return;
            }

            if (activeVerificationCodes[account] == verificationCode)
            {
                account.IsVerified = true;
                Redirect(context, "index");
            }
            else
                Redirect(context, $"verify_landing/verif_code?=\"{data["verifcode"]}\"");
        }
    }
}