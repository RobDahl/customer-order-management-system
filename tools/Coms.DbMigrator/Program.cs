using System;
using System.Linq;
using DbUp.Engine;

namespace Coms.DbMigrator
{
    internal static class Program
    {
        private const string DefaultConnection =
            "Server=(localdb)\\MSSQLLocalDB;Database=Coms;Integrated Security=true;TrustServerCertificate=true";

        private static int Main(string[] args)
        {
            Options options;
            try
            {
                options = Options.Parse(args);
            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine(ex.Message);
                PrintUsage();
                return 2;
            }

            if (options.ShowHelp)
            {
                PrintUsage();
                return 0;
            }

            string connectionString = options.ConnectionString
                ?? Environment.GetEnvironmentVariable("COMS_CONNECTION")
                ?? DefaultConnection;

            Console.WriteLine("Target: " + Describe(connectionString));

            try
            {
                if (options.Drop)
                {
                    MigrationRunner.Drop(connectionString);
                    Console.WriteLine("Database dropped.");
                }

                if (options.Migrate)
                {
                    MigrationRunner.EnsureCreated(connectionString);

                    if (!Report(MigrationRunner.Migrate(connectionString, logToConsole: true), "Migrations"))
                    {
                        return 1;
                    }
                }

                if (options.Seed)
                {
                    if (!Report(MigrationRunner.Seed(connectionString, logToConsole: true), "Seed"))
                    {
                        return 1;
                    }
                }

                Console.WriteLine("Done.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Failed: " + ex.Message);
                return 1;
            }
        }

        private static bool Report(DatabaseUpgradeResult result, string label)
        {
            if (!result.Successful)
            {
                Console.Error.WriteLine(label + " failed: " + result.Error?.Message);
                if (result.ErrorScript != null)
                {
                    Console.Error.WriteLine("Script: " + result.ErrorScript.Name);
                }

                return false;
            }

            Console.WriteLine(label + ": " + result.Scripts.Count() + " script(s) applied.");
            return true;
        }

        private static string Describe(string connectionString)
        {
            // Show server and database only; never echo credentials.
            var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString);
            return builder.DataSource + " / " + builder.InitialCatalog;
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage: Coms.DbMigrator [--migrate] [--seed] [--drop] [--connection <string>]");
            Console.WriteLine();
            Console.WriteLine("  --migrate      Apply pending scripts from db/migrations (default when no other action is given).");
            Console.WriteLine("  --seed         Run db/seed scripts. Implies --migrate unless --drop is given without --migrate.");
            Console.WriteLine("  --drop         Drop the database first. Development only.");
            Console.WriteLine("  --connection   Connection string. Overrides the COMS_CONNECTION environment variable and the LocalDB default.");
            Console.WriteLine("  --help         Show this text.");
        }

        private sealed class Options
        {
            public bool Migrate { get; private set; }
            public bool Seed { get; private set; }
            public bool Drop { get; private set; }
            public bool ShowHelp { get; private set; }
            public string? ConnectionString { get; private set; }

            public static Options Parse(string[] args)
            {
                var options = new Options();
                bool migrateRequested = false;

                for (int i = 0; i < args.Length; i++)
                {
                    switch (args[i])
                    {
                        case "--migrate":
                            migrateRequested = true;
                            break;
                        case "--seed":
                            options.Seed = true;
                            break;
                        case "--drop":
                            options.Drop = true;
                            break;
                        case "--connection":
                            if (i + 1 >= args.Length)
                            {
                                throw new ArgumentException("--connection requires a value.");
                            }

                            options.ConnectionString = args[++i];
                            break;
                        case "--help":
                        case "-h":
                        case "/?":
                            options.ShowHelp = true;
                            break;
                        default:
                            throw new ArgumentException("Unknown argument: " + args[i]);
                    }
                }

                // Migrations run unless the only action requested was a drop.
                options.Migrate = migrateRequested || options.Seed || !options.Drop;
                return options;
            }
        }
    }
}
