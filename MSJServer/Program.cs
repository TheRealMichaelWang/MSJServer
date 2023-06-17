namespace MSJServer 
{
    partial class Server
    {
        private static bool SetAccountPerms(Account account, string perm)
        {
            switch (perm)
            {
                case "e":
                case "editor":
                    account.Permissions = Permissions.Editor;
                    return true;
                case "a":
                case "admin":
                    account.Permissions = Permissions.Admin;
                    return true;
                case "s":
                case "c":
                case "contributor":
                    account.Permissions = Permissions.Contributor;
                    return true;
                default:
                    return false;
            }
        }

        public void RemoveUserCommand(string[] args)
        {
            if (RemoveAccount(args[1]))
                Console.WriteLine($"{args[1]} succesfully removed.");
            else
                Console.WriteLine($"Couldn't remove user {args[1]}.");
        }

        public void SetPermsCommand(string[] args)
        {
            if (!accounts.ContainsKey(args[1]))
            {
                Console.WriteLine($"No such user {args[1]}.");
                return;
            }

            Account account = accounts[args[1]];
            if (!SetAccountPerms(account, args[2]))
                Console.WriteLine($"Unrecognized permission level {args[2]}.");
            else
                Console.WriteLine($"{account.Name}'s permission sucesfully set.");
        }

        public void VerifyUserCommand(string[] args)
        {
            if (!accounts.ContainsKey(args[1]))
            {
                Console.WriteLine($"No such user {args[1]}.");
                return;
            }

            Account account = accounts[args[1]];
            account.IsVerified = true;

            Console.WriteLine($"Succesfully verified {account.Name}.");
        }

        public void ListUsersCommand(string[] args)
        {
            foreach (string key in accounts.Keys)
                Console.WriteLine(key);
        }

        public void GetUserInfoCommand(string[] args)
        {
            if (!accounts.ContainsKey(args[1]))
            {
                Console.WriteLine($"No such user {args[1]}.");
                return;
            }

            Account account = accounts[args[1]];
            Console.WriteLine($"Name: {account.Name}");
            Console.WriteLine($"Perms: {PermissionsHelper.GetDescription(account.Permissions)}");
            Console.WriteLine($"Email: {account.Email}");
            Console.WriteLine($"Created: {account.CreationDate}");
            Console.WriteLine($"IsVerified: {account.IsVerified}");
        }

        public void NotifyUserCommand(string[] args)
        {
            if (!accounts.ContainsKey(args[1]))
            {
                Console.WriteLine($"No such user {args[1]}.");
                return;
            }

            Account account = accounts[args[1]];

            if (!account.IsVerified)
                Console.WriteLine("The user isn't verified, so their email might not work.");

            if (EmailNotifier.Notify(account.Email, args.Length >= 4 ? args[3] : "MSJ Console-Invoked Notification", args[2]))
                Console.WriteLine($"Succesfully notified {account.Name}.");
            else
                Console.WriteLine($"An error occured while ending the notification email to {account.Name}.");
        }
    }

    static class Program
    {
        //indicates whether the program is running in a production environment or not
        public static bool IsProduction { get; private set; }
        
        public static DateTime StartupTime { get; private set; }

        private static Server server;
        private static bool stop = false;
        private static Dictionary<string, Action<string[]>> commands;

        static Program()
        {
            static bool validateAccount(Account account)
            {
                //accounts that are verified, or were created before 6/17/23 are OK
                //new accounts created afterwards must be verified within 7 days.

                if (account.IsVerified || account.CreationDate < new DateTime(2023, 6, 17))
                    return true;

                return StartupTime.Subtract(account.CreationDate).Days < 7;
            }

            server = new Server(validateAccount);
            commands = new()
            {
                {"stop", Stop},
                {"perm", server.SetPermsCommand},
                {"users", server.ListUsersCommand},
                {"notify", server.NotifyUserCommand},
                {"info", server.GetUserInfoCommand},
                {"verify", server.VerifyUserCommand},
                {"remove", server.RemoveUserCommand}
            };
        }

        private static void Stop(string[] args)
        {
            server.Stop();
            stop = true;
        }

        public static void Main(string[] args)
        {
            StartupTime = DateTime.Now;
            IsProduction = args.Contains("--prod") || !args.Contains("--test");

            if (IsProduction)
            {
                if (!Directory.Exists("articles"))
                    Directory.CreateDirectory("articles");
                if (!Directory.Exists("comments"))
                    Directory.CreateDirectory("comments");

                Console.WriteLine("Starting...");
                server.Start();
            }
            else
                Console.WriteLine("Skiping server start...");

            while (!stop)
            {
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
                string input = Console.ReadLine();
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                string[] commandArgs = input.Split('"')
                                           .Select((element, index) => index % 2 == 0  // If even index
                                           ? element.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)  // Split the item
                                           : new string[] { element })  // Keep the entire item
                                           .SelectMany(element => element).ToArray();
#pragma warning restore CS8602 // Dereference of a possibly null reference.
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
                if (commandArgs.Length < 1)
                    continue;

                if (commands.ContainsKey(commandArgs[0]))
                {
                    try
                    {
                        commands[commandArgs[0]].Invoke(commandArgs);
                    }
                    catch (IndexOutOfRangeException)
                    {
                        Console.WriteLine("Command failed; Insufficient arguments.");
                    }
                }
                else
                    Console.WriteLine($"Unrecognized command/operation {commandArgs[0]}.");
            }
        }
    } 
}