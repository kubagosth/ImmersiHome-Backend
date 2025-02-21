using DbUp;
using System.Reflection;

namespace ImmersiHome_API.Persistence.Migrations
{
    // TODO - Add migrations?
    public static class DatabaseInitializer
    {
        /// <summary>
        /// Ensures that the database is created and updated by applying all pending migration scripts.
        /// </summary>
        /// <param name="connectionString">The connection string for database.</param>
        public static void EnsureDatabaseIsUpToDate(string connectionString)
        {
            // Configure the upgrader for PostgreSQL (adjust if using a different provider).
            var upgrader =
                DeployChanges.To
                    .PostgresqlDatabase(connectionString)
                    // Only load scripts from a specific folder namespace (adjust as needed).
                    .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly(), script => script.StartsWith("ImmersiHome_API.Migrations.Scripts"))
                    .LogToConsole() // Logging for debugging; replace or extend with a proper logging framework in production.
                    .Build();

            var result = upgrader.PerformUpgrade();

            if (!result.Successful)
            {
                // Log the error and stop the application startup if migration fails.
                Console.Error.WriteLine(result.Error);
                throw new Exception("Database migration failed", result.Error);
            }

            Console.WriteLine("Database migration successful. Database is up to date.");
        }
    }
}
