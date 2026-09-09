using System;
using DbUp;
using DbUp.Builder;
using DbUp.Engine;
using DbUp.Helpers;

namespace Coms.DbMigrator
{
    /// <summary>
    /// The migrator's operations as a library, so the integration tests can
    /// build a database the same way the command line does.
    /// </summary>
    public static class MigrationRunner
    {
        private const string MigrationPrefix = "Coms.DbMigrator.Migrations.";
        private const string SeedPrefix = "Coms.DbMigrator.Seed.";

        public static void Drop(string connectionString)
        {
            DropDatabase.For.SqlDatabase(connectionString);
        }

        public static void EnsureCreated(string connectionString)
        {
            EnsureDatabase.For.SqlDatabase(connectionString);
        }

        /// <summary>Applies pending scripts from db/migrations, journaled in dbo.SchemaVersions.</summary>
        public static DatabaseUpgradeResult Migrate(string connectionString, bool logToConsole)
        {
            UpgradeEngineBuilder builder = DeployChanges.To
                .SqlDatabase(connectionString)
                .WithScriptsEmbeddedInAssembly(
                    typeof(MigrationRunner).Assembly,
                    name => name.StartsWith(MigrationPrefix, StringComparison.Ordinal))
                .WithTransactionPerScript()
                .WithExecutionTimeout(TimeSpan.FromMinutes(10))
                .JournalToSqlTable("dbo", "SchemaVersions");

            return (logToConsole ? builder.LogToConsole() : builder.LogToNowhere()).Build().PerformUpgrade();
        }

        /// <summary>Runs db/seed scripts. They are re-runnable and never journaled.</summary>
        public static DatabaseUpgradeResult Seed(string connectionString, bool logToConsole)
        {
            UpgradeEngineBuilder builder = DeployChanges.To
                .SqlDatabase(connectionString)
                .WithScriptsEmbeddedInAssembly(
                    typeof(MigrationRunner).Assembly,
                    name => name.StartsWith(SeedPrefix, StringComparison.Ordinal))
                .WithTransactionPerScript()
                .WithExecutionTimeout(TimeSpan.FromMinutes(10))
                .JournalTo(new NullJournal());

            return (logToConsole ? builder.LogToConsole() : builder.LogToNowhere()).Build().PerformUpgrade();
        }

        /// <summary>Drop, create, migrate and seed: a clean database in one call. Throws on failure.</summary>
        public static void Rebuild(string connectionString, bool seed)
        {
            Drop(connectionString);
            EnsureCreated(connectionString);

            DatabaseUpgradeResult migrated = Migrate(connectionString, logToConsole: false);
            if (!migrated.Successful)
            {
                throw new InvalidOperationException("Migration failed: " + migrated.Error?.Message, migrated.Error);
            }

            if (seed)
            {
                DatabaseUpgradeResult seeded = Seed(connectionString, logToConsole: false);
                if (!seeded.Successful)
                {
                    throw new InvalidOperationException("Seed failed: " + seeded.Error?.Message, seeded.Error);
                }
            }
        }
    }
}
