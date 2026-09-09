using System;

namespace Coms.Data.Tests
{
    /// <summary>
    /// Locates the SQL Server used by integration tests. Locally this is
    /// LocalDB; in CI the COMS_TEST_CONNECTION variable points at a
    /// container. Tests that need the database call <see cref="IsAvailable"/>
    /// and skip cleanly when nothing is configured rather than failing.
    /// </summary>
    internal static class TestDatabase
    {
        public const string EnvironmentVariable = "COMS_TEST_CONNECTION";

        private const string LocalDbConnection =
            "Server=(localdb)\\MSSQLLocalDB;Database=Coms_Test;Integrated Security=true;TrustServerCertificate=true";

        public static string ConnectionString
        {
            get
            {
                string? configured = Environment.GetEnvironmentVariable(EnvironmentVariable);
                return string.IsNullOrWhiteSpace(configured) ? LocalDbConnection : configured;
            }
        }

        public static bool IsAvailable
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvironmentVariable)))
                {
                    return true;
                }

                return OperatingSystem.IsWindows();
            }
        }
    }
}
