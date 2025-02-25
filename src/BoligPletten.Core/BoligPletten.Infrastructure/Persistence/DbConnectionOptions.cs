namespace BoligPletten.Infrastructure.Persistence
{
    /// <summary>
    /// Configuration options for database connections
    /// </summary>
    public class DbConnectionOptions
    {
        public string ConnectionString { get; set; } = string.Empty;
        public int MaxPoolSize { get; set; } = 1000; // Default to 1000
        public int MinPoolSize { get; set; } = 10; // Default to 10
        public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromSeconds(30);
    }
}
