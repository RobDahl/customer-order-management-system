using Coms.Application;

namespace Coms.Desktop
{
    /// <summary>
    /// Facts about the running client that the status bar and audit
    /// columns need: who is logged on and which database is in use.
    /// </summary>
    public sealed class DesktopSession : ICurrentUser
    {
        public DesktopSession(string userName, string databaseName)
        {
            UserName = userName;
            DatabaseName = databaseName;
        }

        public string UserName { get; }

        public string DatabaseName { get; }
    }
}
