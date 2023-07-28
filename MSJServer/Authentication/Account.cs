using System.Text;

namespace MSJServer
{
    partial class Server
    {
        public delegate bool AccountValidator(Account account);

        private static Dictionary<Account, long> accountOffsets = new Dictionary<Account, long>();
        private static Dictionary<Account, long> accountSizes = new Dictionary<Account, long>();
        private static long accountSize;

        //accounts that aren't validated by the validator are deleted
        private static Dictionary<string, Account> LoadAccounts(AccountValidator validator)
        {
            accountOffsets.Clear();
            accountSizes.Clear();

            if (!File.Exists("accounts.db"))
            {
                using (FileStream stream = File.Create("accounts.size"))
                using (BinaryWriter writer = new BinaryWriter(stream))
                    writer.Write(0);
                File.Create("accounts.db").Close();

                Logger.Log(Logger.Severity.Information, "No account database found, created one just now.");

                return new Dictionary<string, Account>();
            }

            int count;
            using (FileStream stream = new FileStream("accounts.size", FileMode.Open, FileAccess.Read))
            using (BinaryReader reader = new BinaryReader(stream))
                count = reader.ReadInt32();

            Logger.Log(Logger.Severity.Information, $"Found {count} accounts.");

            bool invalidAccountsDetected = false;
            Dictionary<string, Account> loadedAccounts = new Dictionary<string, Account>(count);
            using (FileStream stream = new FileStream("accounts.db", FileMode.Open, FileAccess.Read))
            using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8))
            {
                for (int i = 0; i < count; i++)
                {
                    long position = stream.Position;
                    Account loadedAccount = Account.FromReader(reader);

                    if (validator(loadedAccount))
                    {
                        loadedAccounts.Add(loadedAccount.Name, loadedAccount);
                        loadedAccounts.Add(loadedAccount.Email, loadedAccount);
                        accountOffsets.Add(loadedAccount, position);
                        accountSizes.Add(loadedAccount, stream.Position - position);
                    }
                    else
                        invalidAccountsDetected = true;
                }
                accountSize = stream.Position;
            }
            Logger.Log(Logger.Severity.Information, $"Finished loading {count} account(s), {accountOffsets} bytes.");

            if (Account.DatabaseVersion < Account.LatestDatabaseVersion || invalidAccountsDetected)
            {
                //reformat and upgrade database format/overwrite database
                using (FileStream stream = new FileStream("accounts.db", FileMode.Open, FileAccess.Write))
                using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8))
                {
                    foreach (Account loadedAccount in accountSizes.Keys)
                    {
                        if (validator(loadedAccount))
                        {
                            long position = stream.Position;
                            loadedAccount.WriteTo(writer);
                            accountOffsets[loadedAccount] = position;
                            accountSizes[loadedAccount] = stream.Position - position;
                        }
                        else
                            Logger.Log(Logger.Severity.Information, $"Removing account per policies.", loadedAccount.Name);
                    }
                    accountSize = stream.Position;
                }

                if (!invalidAccountsDetected)
                {
                    Logger.Log(Logger.Severity.Information, $"Account database upgraded from {Account.DatabaseVersion} to {Account.LatestDatabaseVersion}.");
                    //update database version
                    Account.DatabaseVersion = Account.LatestDatabaseVersion;
                }
            }
            return loadedAccounts;
        }

        private static void RegisterAccount(Account account)
        {
            using (FileStream stream = new FileStream("accounts.size", FileMode.Open, FileAccess.ReadWrite))
            {
                int count;
                using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, true))
                    count = reader.ReadInt32();
                stream.Position = 0;
                using (BinaryWriter writer = new BinaryWriter(stream))
                    writer.Write(count + 1);
            }
            using (FileStream stream = new FileStream("accounts.db", FileMode.Open, FileAccess.Write))
            using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8))
            {
                long oldPosition = accountSize;
                stream.Position = oldPosition;
                accountOffsets.Add(account, oldPosition);
                account.WriteTo(writer);
                accountSizes.Add(account, stream.Position - oldPosition);
                accountSize = stream.Position;
            }

            Directory.CreateDirectory(Path.Combine("users", account.Name));
        }

        public static void RemoveAccount(Account account)
        {
            using (FileStream stream = new FileStream("accounts.size", FileMode.Open, FileAccess.ReadWrite))
            {
                int count;
                using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, true))
                    count = reader.ReadInt32();
                stream.Position = 0;
                using (BinaryWriter writer = new BinaryWriter(stream))
                    writer.Write(count - 1);
            }
            using (FileStream stream = new FileStream("accounts.db", FileMode.Open, FileAccess.ReadWrite))
            {
                long position = accountOffsets[account];
                long oldSize = accountSizes[account];

                //copy data from afer account
                byte[] data = new byte[accountSize - (position + oldSize)];
                stream.Position = position + oldSize;
                stream.Read(data, 0, data.Length);

                using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    stream.Position = position;
                    writer.Write(data);
                    accountSize = stream.Position;
                }

                accountOffsets.Remove(account);
                accountSizes.Remove(account);
            }

            Directory.Delete(Path.Combine("users", account.Name), true);
        }

        public static void ModifyAccount(Account account)
        {
            using (FileStream stream = new FileStream("accounts.db", FileMode.Open, FileAccess.ReadWrite))
            {
                long position = accountOffsets[account];
                long oldSize = accountSizes[account];
                
                //copy data from afer account
                byte[] data = new byte[accountSize - (position + oldSize)];
                stream.Position = position + oldSize;
                stream.Read(data, 0, data.Length);

                stream.Position = position;
                using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    account.WriteTo(writer);
                    long newSize = stream.Position - position;

                    if (newSize != oldSize)
                    {
                        accountSizes[account] = newSize;
                        writer.Write(data);
                    }
                }
            }
        }

        private Dictionary<string, Account> accounts;

        public Account? RegisterAccount(string username, string password, string email)
        {
            if (accounts.ContainsKey(username) || accounts.ContainsKey(email))
                return null;

            Account account = new Account(username, password, email, Permissions.Contributor, DateTime.Now, false);
            accounts.Add(username, account);
            accounts.Add(email, account);
            RegisterAccount(account);
            return account;
        }

        public bool RemoveAccount(string username)
        {
            if (!accounts.ContainsKey(username))
                return false;

            Account account = accounts[username];
            if (account.Permissions >= Permissions.Admin)
                return false;

            RemoveAccount(account);

            accounts.Remove(account.Name);
            accounts.Remove(account.Email);

            return true;
        }
    }

    public class Account
    {
        public static int LatestDatabaseVersion => 1;

        public static int DatabaseVersion
        {
            get
            {
                if (File.Exists("ACCOUNTDB_VER.txt"))
                    return int.Parse(File.ReadAllText("ACCOUNTDB_VER.txt"));
                else
                {
                    File.Create("ACCOUNTDB_VER.txt").Close();
                    File.WriteAllText("ACCOUNTDB_VER.txt", "0");
                    return 0;
                }
            }
            set => File.WriteAllText("ACCOUNTDB_VER.txt", value.ToString());
        }

        public static Account FromReader(BinaryReader reader)
        {
            if(DatabaseVersion == LatestDatabaseVersion)
            {
                return new Account(reader.ReadString(), reader.ReadString(), reader.ReadString(), PermissionsHelper.FromByte(reader.ReadByte()), new DateTime(reader.ReadInt64()), reader.ReadBoolean());
            }

            return new Account(reader.ReadString(), reader.ReadString(), reader.ReadString(), PermissionsHelper.FromByte(reader.ReadByte()), new DateTime(reader.ReadInt64()), false);
        }

        public string Name { get; private set; }
        public DateTime CreationDate { get; private set; }

        private string _password;
        private string _email;
        private Permissions _permissions;
        private bool _verified;
        public string Password { get => _password; set { _password = value; Server.ModifyAccount(this); } }
        public string Email { get => _email; set { _email = value; _verified = false; Server.ModifyAccount(this); } }
        public Permissions Permissions { get => _permissions; set { _permissions = value; Server.ModifyAccount(this); } }
        public bool IsVerified { get => _verified; set { _verified = value; Server.ModifyAccount(this); } }

        public bool IsLoggedIn { get; set; }
        public bool ShouldVerify => (Permissions <= Permissions.Editor && !IsVerified);

        public Account(string name, string password, string email, Permissions permissions, DateTime creationDate, bool isVerified)
        {
            Name = name;
            _password = password;
            _email = email;
            _permissions = permissions;
            _verified = isVerified;
            IsLoggedIn = false;
            CreationDate = creationDate;
        }

        public void WriteTo(BinaryWriter writer)
        {
            writer.Write(Name);
            writer.Write(Password);
            writer.Write(Email);
            writer.Write(PermissionsHelper.ToByte(Permissions));
            writer.Write(CreationDate.Ticks);
            writer.Write(IsVerified);
        }
    }
}
