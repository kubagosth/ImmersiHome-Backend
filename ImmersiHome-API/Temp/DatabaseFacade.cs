using Npgsql;
using System.Text;
using System.IO;

namespace ImmersiHome_API.Temp
{
    public class DatabaseFacade
    {
        private readonly string _connectionString;

        public DatabaseFacade(string connectionString)
        {
            _connectionString = connectionString;
        }

        /// <summary>
        /// Quotes an identifier (e.g., table or column name) to prevent SQL injection attacks.
        /// </summary>
        /// <param name="identifier"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        private string QuoteIdentifier(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                throw new ArgumentException("Identifier cannot be null, empty, or whitespace.", nameof(identifier));

            if (identifier.Contains('.'))
            {
                // Split the identifier into parts and quote each part unless it's already quoted
                var parts = identifier.Split('.');
                return string.Join('.', parts.Select(part =>
                    part.StartsWith('"') && part.EndsWith('"') ? part : '"' + part.Replace("\"", "\"\"") + '"'));
            }

            if (identifier.StartsWith('"') && identifier.EndsWith('"'))
            {
                // If the identifier is already quoted, return it as-is
                return identifier;
            }

            return '"' + identifier.Replace("\"", "\"\"") + '"';
        }

        public async Task<List<Dictionary<string, object>>> SelectAsync(string query, Dictionary<string, object> parameters = null)
        {
            if (string.IsNullOrEmpty(query))
                throw new ArgumentException("Query cannot be null or empty.", nameof(query));

            using NpgsqlConnection connection = new(_connectionString);
            await connection.OpenAsync();

            using NpgsqlCommand command = new(query, connection);

            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                }
            }

            using NpgsqlDataReader reader = await command.ExecuteReaderAsync();

            List<Dictionary<string, object>> results = new();

            while (await reader.ReadAsync())
            {
                Dictionary<string, object> row = new();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }
                results.Add(row);
            }

            return results;
        }

        public async Task DeleteAsync(string targetTableName, string whereClause, Dictionary<string, object> parameters = null)
        {
            if (string.IsNullOrEmpty(targetTableName))
                throw new ArgumentException("Table name cannot be null or empty.", nameof(targetTableName));

            if (string.IsNullOrEmpty(whereClause))
                throw new ArgumentException("Where clause cannot be null or empty.", nameof(whereClause));

            string query = $"DELETE FROM {QuoteIdentifier(targetTableName)} WHERE {whereClause};";

            using NpgsqlConnection connection = new(_connectionString);
            await connection.OpenAsync();

            using NpgsqlCommand command = new(query, connection);

            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                }
            }

            await command.ExecuteNonQueryAsync();
        }

        public async Task UpdateAsync(string targetTableName, Dictionary<string, object> columnValueMapping, string whereClause, Dictionary<string, object> parameters = null)
        {
            if (string.IsNullOrEmpty(targetTableName))
                throw new ArgumentException("Table name cannot be null or empty.", nameof(targetTableName));

            if (columnValueMapping == null || columnValueMapping.Count == 0)
                throw new ArgumentException("Column-value mappings cannot be null or empty.", nameof(columnValueMapping));

            if (string.IsNullOrEmpty(whereClause))
                throw new ArgumentException("Where clause cannot be null or empty.", nameof(whereClause));

            StringBuilder setClause = new();
            List<NpgsqlParameter> queryParameters = new();

            foreach (var columnValue in columnValueMapping)
            {
                if (setClause.Length > 0)
                {
                    setClause.Append(", ");
                }

                setClause.Append($"{QuoteIdentifier(columnValue.Key)} = @{columnValue.Key}");
                queryParameters.Add(new NpgsqlParameter($"@{columnValue.Key}", columnValue.Value ?? DBNull.Value));
            }

            string query = $"UPDATE {QuoteIdentifier(targetTableName)} SET {setClause} WHERE {whereClause};";

            using NpgsqlConnection connection = new(_connectionString);
            await connection.OpenAsync();

            using NpgsqlCommand command = new(query, connection);

            command.Parameters.AddRange(queryParameters.ToArray());

            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                }
            }

            await command.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Inserts multiple records into the specified table.
        /// </summary>
        /// <param name="targetTableName"></param>
        /// <param name="records"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public async Task InsertBatchAsync(string targetTableName, IEnumerable<Dictionary<string, object>> records)
        {
            if (string.IsNullOrEmpty(targetTableName))
                throw new ArgumentException("Table name cannot be null or empty.", nameof(targetTableName));

            if (records == null || !records.Any())
                throw new ArgumentException("Records cannot be null or empty.", nameof(records));

            using NpgsqlConnection connection = new(_connectionString);
            await connection.OpenAsync();

            using NpgsqlBinaryImporter writer = connection.BeginBinaryImport($"COPY {QuoteIdentifier(targetTableName)} ({string.Join(", ", records.First().Keys.Select(QuoteIdentifier))}) FROM STDIN (FORMAT BINARY)");
            foreach (var record in records)
            {
                writer.StartRow();
                foreach (var value in record.Values)
                {
                    writer.Write(value ?? DBNull.Value);
                }
            }

            await writer.CompleteAsync();
        }

        /// <summary>
        /// Inserts a single record into the specified table.
        /// </summary>
        /// <param name="targetTableName"></param>
        /// <param name="columnValueMapping"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public async Task InsertAsync(string targetTableName, Dictionary<string, object> columnValueMapping)
        {
            if (string.IsNullOrEmpty(targetTableName))
                throw new ArgumentException("Table name cannot be null or empty.", nameof(targetTableName));

            if (columnValueMapping == null || columnValueMapping.Count == 0)
                throw new ArgumentException("Column-value mappings cannot be null or empty.", nameof(columnValueMapping));

            StringBuilder columnList = new();
            StringBuilder parameterList = new();
            List<NpgsqlParameter> queryParameters = new();

            foreach (var columnValue in columnValueMapping)
            {
                if (columnList.Length > 0)
                {
                    columnList.Append(", ");
                    parameterList.Append(", ");
                }

                columnList.Append(QuoteIdentifier(columnValue.Key));
                parameterList.Append($"@{columnValue.Key}");

                queryParameters.Add(new NpgsqlParameter($"@{columnValue.Key}", columnValue.Value ?? DBNull.Value));
            }

            string query = $"INSERT INTO {QuoteIdentifier(targetTableName)} ({columnList}) VALUES ({parameterList});";

            using NpgsqlConnection connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            using NpgsqlCommand command = new NpgsqlCommand(query, connection);
            command.Parameters.AddRange(queryParameters.ToArray());

            await command.ExecuteNonQueryAsync();
        }
    }
}