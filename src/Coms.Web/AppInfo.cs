using System;
using System.Reflection;
using Microsoft.Data.SqlClient;

namespace Coms.Web
{
    /// <summary>Facts shown in the status bar.</summary>
    public sealed class AppInfo
    {
        public AppInfo(string connectionString, string environmentName)
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            DatabaseName = builder.InitialCatalog;
            ServerName = builder.DataSource;
            EnvironmentName = environmentName;
            Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
        }

        public string DatabaseName { get; }

        public string ServerName { get; }

        public string EnvironmentName { get; }

        public string Version { get; }

        public DateTime ServerTime => DateTime.Now;
    }
}
