using System;
using System.Configuration;
using Coms.Data.Connections;
using Coms.Desktop.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Coms.Desktop
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);

            ServiceProvider services = BuildServices();

            using (services)
            {
                System.Windows.Forms.Application.Run(services.GetRequiredService<MainForm>());
            }
        }

        private static ServiceProvider BuildServices()
        {
            ConnectionStringSettings setting = ConfigurationManager.ConnectionStrings["Coms"];
            if (setting == null || string.IsNullOrWhiteSpace(setting.ConnectionString))
            {
                throw new ConfigurationErrorsException("Connection string 'Coms' is missing from App.config.");
            }

            var services = new ServiceCollection();

            services.AddLogging(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Information);
                builder.AddDebug();
            });

            services.AddSingleton<IDbConnectionFactory>(new SqlConnectionFactory(setting.ConnectionString));
            services.AddSingleton(new DesktopSession(Environment.UserName, DatabaseNameFrom(setting.ConnectionString)));
            services.AddTransient<MainForm>();

            return services.BuildServiceProvider();
        }

        private static string DatabaseNameFrom(string connectionString)
        {
            var builder = new System.Data.Common.DbConnectionStringBuilder { ConnectionString = connectionString };
            object value;
            if (builder.TryGetValue("Database", out value) || builder.TryGetValue("Initial Catalog", out value))
            {
                return Convert.ToString(value);
            }

            return "(unknown)";
        }
    }
}
