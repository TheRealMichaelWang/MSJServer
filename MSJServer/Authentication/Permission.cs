namespace MSJServer
{
    public enum Permissions
    {
        Admin = 2,
        Editor = 1,
        Contributor = 0
    }

    public static class PermissionsHelper
    {
        public static Permissions FromByte(byte b)
        {
            switch(b)
            {
                case 0:
                    return Permissions.Admin;
                case 1:
                    return Permissions.Editor;
                case 2:
                    return Permissions.Contributor;
                default:
                    throw new ArgumentException(nameof(b));
            }
        }

        public static byte ToByte(Permissions permissions)
        {
            switch (permissions)
            {
                case Permissions.Admin:
                    return 0;
                case Permissions.Editor:
                    return 1;
                case Permissions.Contributor:
                    return 2;
            }
            throw new InvalidOperationException();
        }

        public static string GetDescription(Permissions permissions)
        {
            switch (permissions)
            {
                case Permissions.Admin:
                    return "Administrator";
                case Permissions.Editor:
                    return "Editor";
                case Permissions.Contributor:
                    return "Contributor";
            }
            throw new InvalidOperationException();
        }
    }
}