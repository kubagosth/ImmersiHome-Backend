using Npgsql;
using System.Text;

namespace ImmersiHome_API.Data
{
    public class DatabaseFacade
    {
        private readonly string _connectionString;

        public DatabaseFacade(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task InsertBatchAsync(string tableName, IEnumerable<Dictionary<string, object>> rows)
        {
            using NpgsqlConnection connection = new(_connectionString);
            await connection.OpenAsync();

            using NpgsqlTransaction transaction = await connection.BeginTransactionAsync();
            try
            {
                foreach (var row in rows)
                {
                    await InsertAsync(tableName, row);
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task InsertAsync(string tableName, Dictionary<string, object> keyValuePairs)
        {
            if (string.IsNullOrEmpty(tableName))
                throw new ArgumentException("Table name cannot be null or empty.", nameof(tableName));

            if (keyValuePairs == null || keyValuePairs.Count == 0)
                throw new ArgumentException("Key-value pairs cannot be null or empty.", nameof(keyValuePairs));

            StringBuilder columnNames = new();
            StringBuilder parameterNames = new();
            List<NpgsqlParameter> parameters = [];

            foreach (var keyPairs in keyValuePairs)
            {
                if (columnNames.Length > 0)
                {
                    columnNames.Append(", ");
                    parameterNames.Append(", ");
                }

                columnNames.Append(keyPairs.Key);
                parameterNames.Append($"@{keyPairs.Key}");

                parameters.Add(new NpgsqlParameter($"@{keyPairs.Key}", keyPairs.Value ?? DBNull.Value));
            }

            string query = $"INSERT INTO {tableName} ({columnNames}) VALUES ({parameterNames});";

            using NpgsqlConnection connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            using NpgsqlCommand command = new NpgsqlCommand(query, connection);
            command.Parameters.AddRange(parameters.ToArray());

            await command.ExecuteNonQueryAsync();
        }
    }
}
