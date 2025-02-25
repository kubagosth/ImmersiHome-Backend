namespace ImmersiHome_API.Infrastructure.Persistence
{
    /// <summary>
    /// Configuration options for database connections
    /// </summary>
    public class DbConnectionOptions
    {
        public string ConnectionString { get; set; } = string.Empty;
        public int MaxPoolSize { get; set; } = 100;
        public int MinPoolSize { get; set; } = 10;
        public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromSeconds(30);
    }
}
