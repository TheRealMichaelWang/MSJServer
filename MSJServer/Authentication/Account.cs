using System.Text;

namespace MSJServer
{
    partial class Server
    {
        private static Dictionary<Account, long> accountOffsets = new Dictionary<Account, long>();
        private static Dictionary<Account, long> accountSizes = new Dictionary<Account, long>();
        private static long accountSize;

        private static Dictionary<string, Account> LoadAccounts()
        {
            accountOffsets.Clear();
            accountSizes.Clear();

            if (!File.Exists("accounts.db"))
            {
                using (FileStream stream = File.Create("accounts.size"))
                using (BinaryWriter writer = new BinaryWriter(stream))
                    writer.Write(0);
                File.Create("accounts.db").Close();

                return new Dictionary<string, Account>();
            }

            int count;
            using (FileStream stream = new FileStream("accounts.size", FileMode.Open, FileAccess.Read))
            using (BinaryReader reader = new BinaryReader(stream))
                count = reader.ReadInt32();

            using (FileStream stream = new FileStream("accounts.db", FileMode.Open, FileAccess.Read))
            using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8))
            {
                Dictionary<string, Account> loadedAccounts = new Dictionary<string, Account>(count);
                for (int i = 0; i < count; i++)
                {
                    long position = stream.Position;
                    Account loadedAccount = new Account(reader);
                    loadedAccounts.Add(loadedAccount.Name, loadedAccount);
                    loadedAccounts.Add(loadedAccount.Email, loadedAccount);
                    accountOffsets.Add(loadedAccount, position);
                    accountSizes.Add(loadedAccount, stream.Position - position);
                }
                accountSize = stream.Position;
                return loadedAccounts;
            }
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
        }

        public static void ModifyAccount(Account account)
        {
            using (FileStream stream = new FileStream("accounts.db", FileMode.Open, FileAccess.ReadWrite))
            {
                long position = accountOffsets[account];
                
                //copy data from afer account
                byte[] data = new byte[accountSize - (position + accountSizes[account])];
                stream.Position = position + accountSizes[account];
                stream.Read(data, 0, data.Length);

                accountSizes.Remove(account);
                stream.Position = position;
                using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    account.WriteTo(writer);
                    accountSizes.Add(account, stream.Position - position);
                    writer.Write(data);
                }
            }
        }

        private Dictionary<string, Account> accounts;

        public Account? RegisterAccount(string username, string password, string email)
        {
            if (accounts.ContainsKey(username) || accounts.ContainsKey(email))
                return null;

            Account account = new Account(username, password, email, Permissions.Contributor, DateTime.Now);
            accounts.Add(username, account);
            accounts.Add(email, account);
            RegisterAccount(account);
            return account;
        }
    }

    public class Account
    {
        public string Name { get; private set; }
        public DateTime CreationDate { get; private set; }

        private string _password;
        private string _email;
        private Permissions _permissions;
        public string Password { get => _password; set { _password = value; Server.ModifyAccount(this); } }
        public string Email { get => _email; set { _email = value; Server.ModifyAccount(this); } }
        public Permissions Permissions { get => _permissions; set { _permissions = value; Server.ModifyAccount(this); } }

        public bool IsLoggedIn { get; set; }

        public Account(string name, string password, string email, Permissions permissions, DateTime creationDate)
        {
            Name = name;
            _password = password;
            _email = email;
            _permissions = permissions;
            IsLoggedIn = false;
            CreationDate = creationDate;
        }

        public Account(BinaryReader reader) : this(reader.ReadString(), reader.ReadString(), reader.ReadString(), PermissionsHelper.FromByte(reader.ReadByte()), new DateTime(reader.ReadInt64()))
        {

        }

        public void WriteTo(BinaryWriter writer)
        {
            writer.Write(Name);
            writer.Write(Password);
            writer.Write(Email);
            writer.Write(PermissionsHelper.ToByte(Permissions));
            writer.Write(CreationDate.Ticks);
        }
    }
}
