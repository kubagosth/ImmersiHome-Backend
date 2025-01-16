using Npgsql;
using System.Text;
using System.IO;

namespace ImmersiHome_API.Data
{
    public class DatabaseFacade
    {
        private readonly string _connectionString;

        public DatabaseFacade(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task InsertBatchAsync(string targetTableName, IEnumerable<Dictionary<string, object>> records)
        {
            if (string.IsNullOrEmpty(targetTableName))
                throw new ArgumentException("Table name cannot be null or empty.", nameof(targetTableName));

            if (records == null || !records.Any())
                throw new ArgumentException("Records cannot be null or empty.", nameof(records));

            using NpgsqlConnection connection = new(_connectionString);
            await connection.OpenAsync();

            using NpgsqlBinaryImporter writer = connection.BeginBinaryImport($"COPY {targetTableName} ({string.Join(", ", records.First().Keys)}) FROM STDIN (FORMAT BINARY)");
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

                columnList.Append(columnValue.Key);
                parameterList.Append($"@{columnValue.Key}");

                queryParameters.Add(new NpgsqlParameter($"@{columnValue.Key}", columnValue.Value ?? DBNull.Value));
            }

            string query = $"INSERT INTO {targetTableName} ({columnList}) VALUES ({parameterList});";

            using NpgsqlConnection connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            using NpgsqlCommand command = new NpgsqlCommand(query, connection);
            command.Parameters.AddRange(queryParameters.ToArray());

            await command.ExecuteNonQueryAsync();
        }
    }
}
