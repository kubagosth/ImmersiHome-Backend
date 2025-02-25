using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.Options;
using Npgsql;
using System.Data;

namespace BoligPletten.Infrastructure.Persistence
{
    /// <summary>
    /// Policy for pooling database connections
    /// </summary>
    public class DbConnectionPoolPolicy : IPooledObjectPolicy<IDbConnection>
    {
        private readonly DbConnectionOptions _options;

        public DbConnectionPoolPolicy(IOptions<DbConnectionOptions> options)
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));

            if (string.IsNullOrEmpty(_options.ConnectionString))
            {
                throw new ArgumentException("Connection string cannot be null or empty", nameof(options));
            }
        }

        public IDbConnection Create()
        {
            // Create connection string builder to modify the command timeout
            var builder = new NpgsqlConnectionStringBuilder(_options.ConnectionString)
            {
                CommandTimeout = (int)_options.CommandTimeout.TotalSeconds
            };

            // Create and open connection with the modified connection string
            var connection = new NpgsqlConnection(builder.ConnectionString);
            connection.Open();

            return connection;
        }

        public bool Return(IDbConnection obj)
        {
            // Only keep open connections in the pool
            if (obj.State != ConnectionState.Open)
            {
                try { obj.Open(); }
                catch
                {
                    // If we can't reopen the connection, discard it
                    try { obj.Dispose(); } catch { }
                    return false;
                }
            }

            // Check for pending transactions
            try
            {
                if (obj is NpgsqlConnection npgsqlConn)
                {
                    // Instead of checking InTransaction property,
                    // check if we can begin a transaction. If we can't, it means there's
                    // likely an active transaction already
                    try
                    {
                        using var transaction = npgsqlConn.BeginTransaction();
                        transaction.Rollback();
                    }
                    catch
                    {
                        // There's an active transaction, so reset the connection
                        try
                        {
                            npgsqlConn.Close();
                            npgsqlConn.Open();
                        }
                        catch
                        {
                            // If resetting fails, discard the connection
                            return false;
                        }
                    }
                }
            }
            catch
            {
                // If anything goes wrong, discard the connection
                try { obj.Dispose(); } catch { }
                return false;
            }

            return true;
        }
    }
}
