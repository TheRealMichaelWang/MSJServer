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

        public void SetPermsCommand(string[] args)
        {
            if(!accounts.ContainsKey(args[1]))
            {
                Console.WriteLine($"No such user {args[1]}.");
                return;
            }

            Account account = accounts[args[1]];
            if(!SetAccountPerms(account, args[2]))
                Console.WriteLine($"Unrecognized permission level {args[2]}.");
        }
    }

    static class Program
    {
        private static Server server = new Server();
        private static bool stop = false;
        private static Dictionary<string, Action<string[]>> commands = new()
        {
            {"stop", Stop},
            {"perm", server.SetPermsCommand}
        };

        private static void Stop(string[] args)
        {
            server.Stop();
            stop = true;
        }

        public static void Main()
        {
            if (!Directory.Exists("articles"))
                Directory.CreateDirectory("articles");
            if (!Directory.Exists("comments"))
                Directory.CreateDirectory("comments");

            Console.WriteLine("Starting...");
            server.Start();
            while (!stop)
            {
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
                string input = Console.ReadLine();
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                string[] commandArgs = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
                if (commandArgs.Length < 1)
                    continue;

                if (commands.ContainsKey(commandArgs[0])) {
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