using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using ImmersiHome_API.Models.Domain.Common;
using ImmersiHome_API.Models.Entities.Common;
using ImmersiHome_API.Persistence.Mappers;

namespace ImmersiHome_API.Persistence.Repositories.Common
{
    public class GenericRepository<TDomain, TEntity, TKey> : IGenericRepository<TDomain, TKey>
        where TDomain : class, IGenericModel<TKey>, new()
        where TEntity : class, IGenericEntity<TKey>, new()
        where TKey : struct, IEquatable<TKey>
    {
        protected readonly IDbConnection _connection;
        protected readonly IDbTransaction _transaction;
        protected readonly IGenericMapper<TDomain, TEntity, TKey> _mapper;

        // Use the helper class for table and column names.
        private const string IdColumn = EntityReflectionCache<TEntity>.IdColumnName;
        private const string IsDeletedColumn = EntityReflectionCache<TEntity>.IsDeletedColumnName;
        private static readonly string TableName = EntityReflectionCache<TEntity>.TableName;

        public GenericRepository(IDbConnection connection, IDbTransaction transaction, IGenericMapper<TDomain, TEntity, TKey> mapper)
        {
            _connection = connection;
            _transaction = transaction;
            _mapper = mapper;
        }

        #region Helper Methods

        private static Dictionary<string, object?> CreateParametersFromObject(object? parametersObj)
        {
            var parameters = new Dictionary<string, object?>();
            if (parametersObj != null)
            {
                foreach (var prop in parametersObj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    parameters.Add("@" + prop.Name, prop.GetValue(parametersObj));
                }
            }
            return parameters;
        }

        private static void AddParameters(IDbCommand command, IDictionary<string, object?> parameters)
        {
            foreach (var kv in parameters)
            {
                var param = command.CreateParameter();
                param.ParameterName = kv.Key;
                param.Value = kv.Value ?? DBNull.Value;
                command.Parameters.Add(param);
            }
        }

        private static HashSet<string> GetReaderColumns(IDataReader reader)
        {
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < reader.FieldCount; i++)
            {
                columns.Add(reader.GetName(i));
            }
            return columns;
        }

        // Mapping logic using reflection. For further optimization consider caching compiled delegates.
        private static TEntity MapReaderToEntity(IDataReader reader, HashSet<string> columns)
        {
            var entity = new TEntity();
            foreach (var kvp in EntityReflectionCache<TEntity>.EntityProperties)
            {
                if (columns.Contains(kvp.Key))
                {
                    var value = reader[kvp.Key];
                    kvp.Value.SetValue(entity, value == DBNull.Value ? null : value);
                }
            }
            return entity;
        }

        private static DbCommand EnsureDbCommand(IDbCommand command)
        {
            if (command is DbCommand dbCommand)
                return dbCommand;
            throw new InvalidOperationException("Connection does not support async commands.");
        }

        // Centralizes creation of commands with transaction and parameter binding.
        private DbCommand PrepareCommand(string sql, IDictionary<string, object?> parameters)
        {
            var command = _connection.CreateCommand();
            command.Transaction = _transaction;
            command.CommandText = sql;
            AddParameters(command, parameters);
            return EnsureDbCommand(command);
        }

        private async Task<T?> ExecuteScalarAsync<T>(string sql, IDictionary<string, object?> parameters, CancellationToken cancellationToken)
        {
            using (var command = PrepareCommand(sql, parameters))
            {
                var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                if (result == DBNull.Value || result == null)
                    return default;
                return (T)Convert.ChangeType(result, typeof(T));
            }
        }

        private async Task<TEntity?> ExecuteQuerySingleAsync(string sql, IDictionary<string, object?> parameters, CancellationToken cancellationToken)
        {
            using (var command = PrepareCommand(sql, parameters))
            {
                using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    var columns = GetReaderColumns(reader);
                    if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        return MapReaderToEntity(reader, columns);
                    }
                }
            }
            return default;
        }

        private async Task<List<TEntity>> ExecuteQueryListAsync(string sql, IDictionary<string, object?> parameters, CancellationToken cancellationToken)
        {
            using (var command = PrepareCommand(sql, parameters))
            {
                using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    var list = new List<TEntity>();
                    var columns = GetReaderColumns(reader);
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        list.Add(MapReaderToEntity(reader, columns));
                    }
                    return list;
                }
            }
        }

        // Helper for commands that return a list of IDs (used in bulk operations)
        private async Task<List<TKey>> ExecuteIdReturningCommandAsync(string sql, IDictionary<string, object?> parameters, CancellationToken cancellationToken)
        {
            var ids = new List<TKey>();
            using (var command = PrepareCommand(sql, parameters))
            {
                using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        TKey id = (TKey)Convert.ChangeType(reader[0], typeof(TKey));
                        ids.Add(id);
                    }
                }
            }
            return ids;
        }

        #endregion

        #region Repository Methods

        private async Task<TEntity?> GetEntityByIdAsync(TKey id, CancellationToken cancellationToken = default)
        {
            string sql = $"SELECT * FROM {TableName} WHERE {IdColumn} = @Id AND {IsDeletedColumn} = FALSE;";
            var parameters = new Dictionary<string, object?> { { "@Id", id } };
            return await ExecuteQuerySingleAsync(sql, parameters, cancellationToken).ConfigureAwait(false);
        }

        public async Task<TDomain> AddAsync(TDomain model, CancellationToken cancellationToken = default)
        {
            var entity = _mapper.MapToEntity(model);
            var columns = EntityReflectionCache<TEntity>.GetColumns(includeId: false).ToList();
            string columnList = string.Join(", ", columns);
            string parameterList = string.Join(", ", columns.Select(c => "@" + c));
            string sql = $"INSERT INTO {TableName} ({columnList}) VALUES ({parameterList}) RETURNING {IdColumn};";

            var parameters = columns.ToDictionary(col => "@" + col,
                col => entity.GetType().GetProperty(col)?.GetValue(entity));

            TKey id = await ExecuteScalarAsync<TKey>(sql, parameters, cancellationToken).ConfigureAwait(false);
            EntityReflectionCache<TEntity>.IdProperty?.SetValue(entity, id);
            return _mapper.MapToModel(entity);
        }

        public async Task<TDomain> UpdateAsync(TDomain model, CancellationToken cancellationToken = default)
        {
            var existing = await GetEntityByIdAsync(model.Id, cancellationToken).ConfigureAwait(false);
            if (existing == null)
                throw new EntityNotFoundException($"Entity with id {model.Id} not found.");

            var updated = _mapper.MapToEntity(model);
            // Preserve immutable values.
            updated.CreatedUtc = existing.CreatedUtc;
            updated.IsDeleted = existing.IsDeleted;
            updated.ModelUpdated();

            var columns = EntityReflectionCache<TEntity>.GetColumns(includeId: false).ToList();
            string setClause = string.Join(", ", columns.Select(c => $"{c} = @{c}"));
            string sql = $"UPDATE {TableName} SET {setClause} WHERE {IdColumn} = @Id RETURNING {IdColumn};";

            var parameters = columns.ToDictionary(col => "@" + col,
                col => updated.GetType().GetProperty(col)?.GetValue(updated));
            parameters.Add("@Id", updated.Id);

            TKey id = await ExecuteScalarAsync<TKey>(sql, parameters, cancellationToken).ConfigureAwait(false);
            EntityReflectionCache<TEntity>.IdProperty?.SetValue(updated, id);
            return _mapper.MapToModel(updated);
        }

        public async Task<TDomain> UpsertAsync(TDomain model, CancellationToken cancellationToken = default)
        {
            var entity = _mapper.MapToEntity(model);
            var columns = EntityReflectionCache<TEntity>.GetColumns(includeId: false).ToList();
            string columnList = string.Join(", ", columns);
            string parameterList = string.Join(", ", columns.Select(c => "@" + c));
            string updateSet = string.Join(", ", columns.Select(c => $"{c} = EXCLUDED.{c}"));
            string sql = $"INSERT INTO {TableName} ({columnList}) VALUES ({parameterList}) " +
                         $"ON CONFLICT ({IdColumn}) DO UPDATE SET {updateSet} RETURNING {IdColumn};";

            var parameters = columns.ToDictionary(col => "@" + col,
                col => entity.GetType().GetProperty(col)?.GetValue(entity));

            TKey id = await ExecuteScalarAsync<TKey>(sql, parameters, cancellationToken).ConfigureAwait(false);
            EntityReflectionCache<TEntity>.IdProperty?.SetValue(entity, id);
            return _mapper.MapToModel(entity);
        }

        public async Task<TDomain?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default)
        {
            string sql = $"SELECT * FROM {TableName} WHERE {IdColumn} = @Id AND {IsDeletedColumn} = FALSE;";
            var parameters = new Dictionary<string, object?> { { "@Id", id } };
            var entity = await ExecuteQuerySingleAsync(sql, parameters, cancellationToken).ConfigureAwait(false);
            return entity != null ? _mapper.MapToModel(entity) : default;
        }

        public async Task<bool> ExistsAsync(TKey id, CancellationToken cancellationToken = default)
        {
            string sql = $"SELECT COUNT(1) FROM {TableName} WHERE {IdColumn} = @Id AND {IsDeletedColumn} = FALSE;";
            var parameters = new Dictionary<string, object?> { { "@Id", id } };
            int count = await ExecuteScalarAsync<int>(sql, parameters, cancellationToken).ConfigureAwait(false);
            return count > 0;
        }

        public async Task<int> CountAsync(CancellationToken cancellationToken = default)
        {
            string sql = $"SELECT COUNT(1) FROM {TableName} WHERE {IsDeletedColumn} = FALSE;";
            var parameters = new Dictionary<string, object?>();
            return await ExecuteScalarAsync<int>(sql, parameters, cancellationToken).ConfigureAwait(false);
        }

        public async Task<bool> SoftDeleteAsync(TKey id, CancellationToken cancellationToken = default)
        {
            var entity = await GetEntityByIdAsync(id, cancellationToken).ConfigureAwait(false);
            if (entity == null)
                throw new EntityNotFoundException($"Entity with id {id} not found.");

            entity.MarkAsDeleted();
            var columns = EntityReflectionCache<TEntity>.GetColumns(includeId: false).ToList();
            string setClause = string.Join(", ", columns.Select(c => $"{c} = @{c}"));
            string sql = $"UPDATE {TableName} SET {setClause} WHERE {IdColumn} = @Id RETURNING {IdColumn};";
            var parameters = columns.ToDictionary(col => "@" + col,
                col => entity.GetType().GetProperty(col)?.GetValue(entity));
            parameters.Add("@Id", id);

            TKey affectedId = await ExecuteScalarAsync<TKey>(sql, parameters, cancellationToken).ConfigureAwait(false);
            return !EqualityComparer<TKey>.Default.Equals(affectedId, default);
        }

        public async Task<bool> DeleteAsync(TKey id, CancellationToken cancellationToken = default)
        {
            string sql = $"DELETE FROM {TableName} WHERE {IdColumn} = @Id RETURNING {IdColumn};";
            var parameters = new Dictionary<string, object?> { { "@Id", id } };
            TKey affectedId = await ExecuteScalarAsync<TKey>(sql, parameters, cancellationToken).ConfigureAwait(false);
            return !EqualityComparer<TKey>.Default.Equals(affectedId, default);
        }

        public async Task<IEnumerable<TKey>> BulkUpsertAsync(IEnumerable<TDomain> models, CancellationToken cancellationToken = default)
        {
            if (!models.Any())
                return Enumerable.Empty<TKey>();

            var entities = models.Select(m => _mapper.MapToEntity(m)).ToList();
            var columns = EntityReflectionCache<TEntity>.GetColumns(includeId: false).ToList();

            var sqlBuilder = new StringBuilder();
            sqlBuilder.Append($"INSERT INTO {TableName} ({string.Join(", ", columns)}) VALUES ");
            var parameters = new Dictionary<string, object?>();
            var valueRows = new List<string>();
            int rowIndex = 0;
            foreach (var entity in entities)
            {
                var paramNames = columns.Select(col =>
                {
                    string paramName = $"@{col}_{rowIndex}";
                    parameters.Add(paramName, entity.GetType().GetProperty(col)?.GetValue(entity));
                    return paramName;
                });
                valueRows.Add($"({string.Join(", ", paramNames)})");
                rowIndex++;
            }
            sqlBuilder.Append(string.Join(", ", valueRows));
            string updateSet = string.Join(", ", columns.Select(c => $"{c} = EXCLUDED.{c}"));
            sqlBuilder.Append($" ON CONFLICT ({IdColumn}) DO UPDATE SET {updateSet} RETURNING {IdColumn};");
            string sql = sqlBuilder.ToString();

            var returnedIds = await ExecuteIdReturningCommandAsync(sql, parameters, cancellationToken).ConfigureAwait(false);
            for (int i = 0; i < entities.Count && i < returnedIds.Count; i++)
            {
                EntityReflectionCache<TEntity>.IdProperty?.SetValue(entities[i], returnedIds[i]);
            }
            return returnedIds;
        }

        public async Task<IEnumerable<TKey>> BulkSoftDeleteAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default)
        {
            string sql = $"UPDATE {TableName} SET {IsDeletedColumn} = TRUE WHERE {IdColumn} = ANY(@Ids) RETURNING {IdColumn};";
            var parameters = new Dictionary<string, object?> { { "@Ids", ids.ToArray() } };
            return await ExecuteIdReturningCommandAsync(sql, parameters, cancellationToken).ConfigureAwait(false);
        }

        public async Task<IEnumerable<TKey>> BulkDeleteAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default)
        {
            string sql = $"DELETE FROM {TableName} WHERE {IdColumn} = ANY(@Ids) RETURNING {IdColumn};";
            var parameters = new Dictionary<string, object?> { { "@Ids", ids.ToArray() } };
            return await ExecuteIdReturningCommandAsync(sql, parameters, cancellationToken).ConfigureAwait(false);
        }

        public async IAsyncEnumerable<TDomain> GetAllAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            string sql = $"SELECT * FROM {TableName} WHERE {IsDeletedColumn} = FALSE;";
            using (var command = PrepareCommand(sql, new Dictionary<string, object?>()))
            {
                using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    var columns = GetReaderColumns(reader);
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        yield return _mapper.MapToModel(MapReaderToEntity(reader, columns));
                    }
                }
            }
        }

        public async IAsyncEnumerable<TDomain> GetPaginatedAsync(
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            int offset = (page - 1) * pageSize;
            string sql = $"SELECT * FROM {TableName} WHERE {IsDeletedColumn} = FALSE LIMIT @PageSize OFFSET @Offset;";
            var parameters = new Dictionary<string, object?> {
                { "@PageSize", pageSize },
                { "@Offset", offset }
            };

            using (var command = PrepareCommand(sql, parameters))
            {
                using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    var columns = GetReaderColumns(reader);
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        yield return _mapper.MapToModel(MapReaderToEntity(reader, columns));
                    }
                }
            }
        }

        public async IAsyncEnumerable<TDomain> GetPaginatedDynamicAsync(
            int page,
            int pageSize,
            string? additionalWhereClause = null,
            object? parametersObj = null,
            string? orderByClause = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // Always enforce the soft-delete condition.
            string baseCondition = $"{IsDeletedColumn} = FALSE";
            string whereClause = !string.IsNullOrWhiteSpace(additionalWhereClause)
                ? $"{baseCondition} AND ({additionalWhereClause})"
                : baseCondition;

            var sqlBuilder = new StringBuilder();
            sqlBuilder.Append($"SELECT * FROM {TableName} WHERE {whereClause}");
            if (!string.IsNullOrWhiteSpace(orderByClause))
                sqlBuilder.Append(" ORDER BY " + orderByClause);
            sqlBuilder.Append(" LIMIT @PageSize OFFSET @Offset;");

            var parameters = CreateParametersFromObject(parametersObj);
            parameters.Add("@PageSize", pageSize);
            parameters.Add("@Offset", (page - 1) * pageSize);

            using (var command = PrepareCommand(sqlBuilder.ToString(), parameters))
            {
                using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    var columns = GetReaderColumns(reader);
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        yield return _mapper.MapToModel(MapReaderToEntity(reader, columns));
                    }
                }
            }
        }

        public async IAsyncEnumerable<TDomain> GetByIdsAsync(IEnumerable<TKey> ids, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            string sql = $"SELECT * FROM {TableName} WHERE {IdColumn} = ANY(@Ids) AND {IsDeletedColumn} = FALSE;";
            var parameters = new Dictionary<string, object?> { { "@Ids", ids.ToArray() } };
            using (var command = PrepareCommand(sql, parameters))
            {
                using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    var columns = GetReaderColumns(reader);
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        yield return _mapper.MapToModel(MapReaderToEntity(reader, columns));
                    }
                }
            }
        }

        public async Task<IEnumerable<TResult>> ExecuteQueryAsync<TResult>(string sql, object? parametersObj = null, CancellationToken cancellationToken = default)
        {
            var parameters = CreateParametersFromObject(parametersObj);
            var results = new List<TResult>();
            using (var command = PrepareCommand(sql, parameters))
            {
                using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        object value = reader[0];
                        results.Add((TResult)(value == DBNull.Value ? default(TResult)! : Convert.ChangeType(value, typeof(TResult))));
                    }
                }
            }
            return results;
        }

        #endregion
    }
}
