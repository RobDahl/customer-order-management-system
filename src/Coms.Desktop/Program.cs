using System;
using System.Configuration;
using System.Globalization;
using Coms.Application;
using Coms.Data;
using Coms.Desktop.Forms;
using Coms.Desktop.Infrastructure;
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

            ServiceProvider services;
            try
            {
                services = BuildServices();
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show(
                    "The application could not start:" + Environment.NewLine + Environment.NewLine + ex.Message,
                    Ui.AppTitle, System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                return;
            }

            using (services)
            {
                ILogger logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
                logger.LogInformation("Desktop client starting as {User}", Environment.UserName);

                System.Windows.Forms.Application.ThreadException += (s, e) =>
                {
                    logger.LogError(e.Exception, "Unhandled UI exception");
                    System.Windows.Forms.MessageBox.Show(
                        "An unexpected error occurred:" + Environment.NewLine + Environment.NewLine + e.Exception.Message,
                        Ui.AppTitle, System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                };

                System.Windows.Forms.Application.Run(services.GetRequiredService<MainForm>());

                logger.LogInformation("Desktop client closed");
            }
        }

        private static ServiceProvider BuildServices()
        {
            ConnectionStringSettings setting = ConfigurationManager.ConnectionStrings["Coms"];
            if (setting == null || string.IsNullOrWhiteSpace(setting.ConnectionString))
            {
                throw new ConfigurationErrorsException("Connection string 'Coms' is missing from App.config.");
            }

            var options = new ComsOptions
            {
                TaxRate = ReadDecimal("Coms:TaxRate", 0.08m),
                AllowNegativeStock = ReadBool("Coms:AllowNegativeStock", false),
                MaxImportRows = ReadInt("Coms:MaxImportRows", 10000)
            };

            string logDirectory = Environment.ExpandEnvironmentVariables(
                ConfigurationManager.AppSettings["Coms:LogDirectory"] ?? "%LOCALAPPDATA%\\Coms\\logs");

            var services = new ServiceCollection();

            services.AddLogging(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Information);
                builder.AddDebug();
                builder.AddProvider(new FileLoggerProvider(logDirectory, LogLevel.Information));
            });

            var session = new DesktopSession(Environment.UserName, DatabaseNameFrom(setting.ConnectionString));

            services.AddSingleton(session);
            services.AddSingleton<ICurrentUser>(session);
            services.AddComsData(setting.ConnectionString);
            services.AddComsApplication(options);
            services.AddSingleton<Scoped>();
            services.AddTransient<MainForm>();

            return services.BuildServiceProvider();
        }

        private static decimal ReadDecimal(string key, decimal fallback)
        {
            decimal value;
            return decimal.TryParse(ConfigurationManager.AppSettings[key], NumberStyles.Number, CultureInfo.InvariantCulture, out value) ? value : fallback;
        }

        private static int ReadInt(string key, int fallback)
        {
            int value;
            return int.TryParse(ConfigurationManager.AppSettings[key], out value) ? value : fallback;
        }

        private static bool ReadBool(string key, bool fallback)
        {
            bool value;
            return bool.TryParse(ConfigurationManager.AppSettings[key], out value) ? value : fallback;
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
