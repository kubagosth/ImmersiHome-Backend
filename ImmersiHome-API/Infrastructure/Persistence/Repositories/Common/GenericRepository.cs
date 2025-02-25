using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ImmersiHome_API.Infrastructure.Mappers;
using ImmersiHome_API.Models.Domain.Common;
using ImmersiHome_API.Models.Entities.Common;

namespace ImmersiHome_API.Infrastructure.Persistence.Repositories.Common
{
    /// <summary>
    /// High-performance generic repository implementation with optimized data access patterns
    /// </summary>
    public class GenericRepository<TDomain, TEntity, TKey> : IGenericRepository<TDomain, TKey>
        where TDomain : class, IGenericModel<TKey>, new()
        where TEntity : class, IGenericEntity<TKey>, new()
        where TKey : struct, IEquatable<TKey>
    {
        protected readonly IDbConnection _connection;
        protected readonly IDbTransaction _transaction;
        protected readonly IGenericMapper<TDomain, TEntity, TKey> _mapper;

        // Cache of prepared SQL queries for better performance
        private static readonly ConcurrentDictionary<string, string> _sqlCache = new ConcurrentDictionary<string, string>();

        // Use the helper class for table and column names
        private static readonly string _tableName = EntityReflectionCache<TEntity>.TableName;
        private static readonly string _idColumn = EntityReflectionCache<TEntity>.IdColumnName;
        private static readonly string _isDeletedColumn = EntityReflectionCache<TEntity>.IsDeletedColumnName;
        private static readonly SqlBuilder _sqlBuilder;

        // Cache frequently used column lists
        private static readonly List<string> _insertColumns;
        private static readonly List<string> _updateColumns;

        // Cached SQL queries
        private static readonly string _selectByIdSql;
        private static readonly string _selectAllSql;
        private static readonly string _selectPaginatedSql;
        private static readonly string _countSql;
        private static readonly string _existsSql;
        private static readonly string _softDeleteSql;
        private static readonly string _hardDeleteSql;
        private static readonly string _selectByIdsSql;
        private static readonly string _bulkSoftDeleteSql;
        private static readonly string _bulkHardDeleteSql;
        private static readonly string _insertSql;
        private static readonly string _updateSql;
        private static readonly string _upsertSql;

        // Static constructor to initialize cached SQL queries
        static GenericRepository()
        {
            _sqlBuilder = SqlBuilder.For<TEntity>();

            // Cache column lists
            _insertColumns = EntityReflectionCache<TEntity>.GetColumns(includeId: false).ToList();
            _updateColumns = EntityReflectionCache<TEntity>.GetColumns(includeId: false).ToList();

            // Pre-compute SQL queries for common operations
            _selectByIdSql = _sqlBuilder.SelectById();
            _selectAllSql = _sqlBuilder.SelectAll();
            _selectPaginatedSql = _sqlBuilder.SelectPaginated();
            _countSql = _sqlBuilder.Count();
            _existsSql = _sqlBuilder.Exists();
            _softDeleteSql = _sqlBuilder.SoftDelete();
            _hardDeleteSql = _sqlBuilder.HardDelete();
            _selectByIdsSql = _sqlBuilder.SelectByIds();
            _bulkSoftDeleteSql = _sqlBuilder.BulkSoftDelete();
            _bulkHardDeleteSql = _sqlBuilder.BulkHardDelete();
            _insertSql = _sqlBuilder.Insert(_insertColumns);
            _updateSql = _sqlBuilder.Update(_updateColumns);
            _upsertSql = _sqlBuilder.Upsert(_insertColumns);
        }

        public GenericRepository(
            IDbConnection connection,
            IDbTransaction transaction,
            IGenericMapper<TDomain, TEntity, TKey> mapper)
        {
            _connection = connection;
            _transaction = transaction;
            _mapper = mapper;
        }

        #region Optimized Helper Methods

        /// <summary>
        /// Creates a dictionary of SQL parameters from an object's public properties
        /// </summary>
        protected virtual Dictionary<string, object?> CreateParametersFromObject(object? parametersObj)
        {
            var parameters = new Dictionary<string, object?>();
            if (parametersObj == null) return parameters;

            // Use cached reflection for better performance
            foreach (var prop in parametersObj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                parameters.Add("@" + prop.Name, prop.GetValue(parametersObj));
            }
            return parameters;
        }

        /// <summary>
        /// Efficiently adds parameters to a command
        /// </summary>
        protected virtual void AddParameters(IDbCommand command, IDictionary<string, object?> parameters)
        {
            // Pre-allocate parameter collection capacity if possible
            if (command.Parameters is IList<IDbDataParameter> paramList)
            {
                if (paramList is List<IDbDataParameter> list)
                {
                    list.Capacity = parameters.Count;
                }
            }

            foreach (var kv in parameters)
            {
                var param = command.CreateParameter();
                param.ParameterName = kv.Key;
                param.Value = kv.Value ?? DBNull.Value;
                command.Parameters.Add(param);
            }
        }

        /// <summary>
        /// Efficiently retrieves the column names from the data reader in order
        /// </summary>
        protected virtual List<string> GetReaderColumns(IDataReader reader)
        {
            var fieldCount = reader.FieldCount;
            var columns = new List<string>(fieldCount);

            for (int i = 0; i < fieldCount; i++)
            {
                columns.Add(reader.GetName(i));
            }
            return columns;
        }

        /// <summary>
        /// Maps a data reader row to an entity with optimized property access in sequential order
        /// </summary>
        protected virtual TEntity MapReaderToEntity(IDataReader reader, List<string> columns)
        {
            var entity = new TEntity();

            // With SequentialAccess, we MUST access columns in order by ordinal
            for (int i = 0; i < reader.FieldCount; i++)
            {
                string columnName = columns[i];

                if (EntityReflectionCache<TEntity>.EntityProperties.TryGetValue(columnName, out var prop))
                {
                    // Get the value for the current column (important: access in order)
                    object value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    prop.SetValue(entity, value);
                }
                else
                {
                    // Must advance through the column even if we don't use it
                    reader.GetValue(i);
                }
            }

            return entity;
        }

        /// <summary>
        /// Creates and prepares a DbCommand with the provided SQL and parameters
        /// </summary>
        protected virtual DbCommand PrepareCommand(string sql, IDictionary<string, object?> parameters)
        {
            var command = _connection.CreateCommand();
            command.Transaction = _transaction;
            command.CommandText = sql;

            AddParameters(command, parameters);

            if (command is DbCommand dbCommand)
            {
                // Consider preparing the command for frequently executed queries
                // dbCommand.Prepare(); 
                return dbCommand;
            }
            else
            {
                throw new InvalidOperationException("Connection does not support async commands.");
            }
        }

        /// <summary>
        /// Executes a scalar query with optimized error handling
        /// </summary>
        protected virtual async Task<T?> ExecuteScalarAsync<T>(string sql, IDictionary<string, object?> parameters, CancellationToken cancellationToken)
        {
            using var command = PrepareCommand(sql, parameters);

            var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

            if (result == DBNull.Value || result == null)
                return default;

            // Optimize type conversion for common types
            if (typeof(T) == typeof(int) && result is int intValue)
                return (T)(object)intValue;

            if (typeof(T) == typeof(long) && result is long longValue)
                return (T)(object)longValue;

            if (typeof(T) == typeof(decimal) && result is decimal decimalValue)
                return (T)(object)decimalValue;

            if (typeof(T) == typeof(bool) && result is bool boolValue)
                return (T)(object)boolValue;

            if (typeof(T) == typeof(Guid) && result is Guid guidValue)
                return (T)(object)guidValue;

            if (typeof(T) == typeof(DateTime) && result is DateTime dateTimeValue)
                return (T)(object)dateTimeValue;

            // Fall back to Convert for other types
            return (T)Convert.ChangeType(result, typeof(T));
        }

        /// <summary>
        /// Executes a query that returns a single entity with optimized resource handling
        /// </summary>
        protected virtual async Task<TEntity?> ExecuteQuerySingleAsync(string sql, IDictionary<string, object?> parameters, CancellationToken cancellationToken)
        {
            using var command = PrepareCommand(sql, parameters);
            using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken).ConfigureAwait(false);

            var columns = GetReaderColumns(reader);

            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return MapReaderToEntity(reader, columns);
            }

            return null;
        }

        /// <summary>
        /// Efficiently executes a query that returns a list of IDs
        /// </summary>
        protected virtual async Task<List<TKey>> ExecuteIdReturningCommandAsync(string sql, IDictionary<string, object?> parameters, CancellationToken cancellationToken)
        {
            using var command = PrepareCommand(sql, parameters);
            using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            // Pre-allocate list with expected capacity if available
            var ids = new List<TKey>();

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                // Optimized conversion based on TKey type
                TKey id;

                if (typeof(TKey) == typeof(int))
                {
                    id = (TKey)(object)reader.GetInt32(0);
                }
                else if (typeof(TKey) == typeof(long))
                {
                    id = (TKey)(object)reader.GetInt64(0);
                }
                else if (typeof(TKey) == typeof(Guid))
                {
                    id = (TKey)(object)reader.GetGuid(0);
                }
                else
                {
                    id = (TKey)Convert.ChangeType(reader[0], typeof(TKey));
                }

                ids.Add(id);
            }

            return ids;
        }

        /// <summary>
        /// Gets parameters dictionary from entity properties with optimized allocations
        /// </summary>
        protected virtual Dictionary<string, object?> GetParametersFromEntity(TEntity entity, IEnumerable<string> columns)
        {
            var entityType = entity.GetType();
            var parameters = new Dictionary<string, object?>(columns.Count());

            foreach (var col in columns)
            {
                var prop = entityType.GetProperty(col);
                parameters.Add("@" + col, prop?.GetValue(entity));
            }

            return parameters;
        }

        #endregion

        #region Repository Methods

        /// <summary>
        /// Gets an entity by ID with optimized query execution
        /// </summary>
        private async Task<TEntity?> GetEntityByIdAsync(TKey id, CancellationToken cancellationToken = default)
        {
            var parameters = new Dictionary<string, object?> { { "@Id", id } };
            return await ExecuteQuerySingleAsync(_selectByIdSql, parameters, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Adds a new entity with optimized SQL generation and parameter handling
        /// </summary>
        public async Task<TDomain> AddAsync(TDomain model, CancellationToken cancellationToken = default)
        {
            var entity = _mapper.MapToEntity(model);

            var parameters = GetParametersFromEntity(entity, _insertColumns);

            TKey id = await ExecuteScalarAsync<TKey>(_insertSql, parameters, cancellationToken).ConfigureAwait(false);
            EntityReflectionCache<TEntity>.IdProperty?.SetValue(entity, id);

            return _mapper.MapToModel(entity);
        }

        /// <summary>
        /// Updates an existing entity with optimized property and SQL handling
        /// </summary>
        public async Task<TDomain> UpdateAsync(TDomain model, CancellationToken cancellationToken = default)
        {
            var existing = await GetEntityByIdAsync(model.Id, cancellationToken).ConfigureAwait(false);
            if (existing == null)
                throw new EntityNotFoundException($"Entity with id {model.Id} not found.");

            var updated = _mapper.MapToEntity(model);

            // Preserve immutable values
            updated.CreatedUtc = existing.CreatedUtc;
            updated.IsDeleted = existing.IsDeleted;
            updated.ModelUpdated();

            var parameters = GetParametersFromEntity(updated, _updateColumns);
            parameters.Add("@Id", updated.Id);

            TKey id = await ExecuteScalarAsync<TKey>(_updateSql, parameters, cancellationToken).ConfigureAwait(false);
            EntityReflectionCache<TEntity>.IdProperty?.SetValue(updated, id);

            return _mapper.MapToModel(updated);
        }

        /// <summary>
        /// Inserts or updates an entity with optimized upsert operation
        /// </summary>
        public async Task<TDomain> UpsertAsync(TDomain model, CancellationToken cancellationToken = default)
        {
            var entity = _mapper.MapToEntity(model);
            var parameters = GetParametersFromEntity(entity, _insertColumns);

            TKey id = await ExecuteScalarAsync<TKey>(_upsertSql, parameters, cancellationToken).ConfigureAwait(false);
            EntityReflectionCache<TEntity>.IdProperty?.SetValue(entity, id);

            return _mapper.MapToModel(entity);
        }

        /// <summary>
        /// Gets a domain model by ID with optimized query execution
        /// </summary>
        public async Task<TDomain?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default)
        {
            var parameters = new Dictionary<string, object?> { { "@Id", id } };
            var entity = await ExecuteQuerySingleAsync(_selectByIdSql, parameters, cancellationToken).ConfigureAwait(false);

            return entity != null ? _mapper.MapToModel(entity) : default;
        }

        /// <summary>
        /// Checks if an entity exists by ID with optimized count query
        /// </summary>
        public async Task<bool> ExistsAsync(TKey id, CancellationToken cancellationToken = default)
        {
            var parameters = new Dictionary<string, object?> { { "@Id", id } };
            int count = await ExecuteScalarAsync<int>(_existsSql, parameters, cancellationToken).ConfigureAwait(false);

            return count > 0;
        }

        /// <summary>
        /// Counts all non-deleted entities with optimized query
        /// </summary>
        public async Task<int> CountAsync(CancellationToken cancellationToken = default)
        {
            var parameters = new Dictionary<string, object?>();
            return await ExecuteScalarAsync<int>(_countSql, parameters, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Marks an entity as deleted with optimized update
        /// </summary>
        public async Task<bool> SoftDeleteAsync(TKey id, CancellationToken cancellationToken = default)
        {
            var parameters = new Dictionary<string, object?> { { "@Id", id } };

            TKey affectedId = await ExecuteScalarAsync<TKey>(_softDeleteSql, parameters, cancellationToken).ConfigureAwait(false);
            return !EqualityComparer<TKey>.Default.Equals(affectedId, default);
        }

        /// <summary>
        /// Permanently deletes an entity with optimized delete operation
        /// </summary>
        public async Task<bool> DeleteAsync(TKey id, CancellationToken cancellationToken = default)
        {
            var parameters = new Dictionary<string, object?> { { "@Id", id } };

            TKey affectedId = await ExecuteScalarAsync<TKey>(_hardDeleteSql, parameters, cancellationToken).ConfigureAwait(false);
            return !EqualityComparer<TKey>.Default.Equals(affectedId, default);
        }

        /// <summary>
        /// Performs an optimized bulk upsert with batched operations
        /// </summary>
        public async Task<IEnumerable<TKey>> BulkUpsertAsync(IEnumerable<TDomain> models, CancellationToken cancellationToken = default)
        {
            // Batch processing for better performance
            const int batchSize = 100;

            if (!models.Any())
                return Enumerable.Empty<TKey>();

            var entities = models.Select(m => _mapper.MapToEntity(m)).ToList();
            var allReturnedIds = new List<TKey>(entities.Count);

            // Process in batches to avoid excessive parameter counts
            for (int i = 0; i < entities.Count; i += batchSize)
            {
                var batch = entities.Skip(i).Take(batchSize).ToList();

                var sqlBuilder = new StringBuilder();
                sqlBuilder.Append($"INSERT INTO {_tableName} ({string.Join(", ", _insertColumns)}) VALUES ");

                var parameters = new Dictionary<string, object?>(batch.Count * _insertColumns.Count);
                var valueRows = new List<string>(batch.Count);

                for (int rowIndex = 0; rowIndex < batch.Count; rowIndex++)
                {
                    var entity = batch[rowIndex];
                    var paramNames = _insertColumns.Select(col =>
                    {
                        string paramName = $"@{col}_{rowIndex}";
                        var prop = entity.GetType().GetProperty(col);
                        parameters.Add(paramName, prop?.GetValue(entity));
                        return paramName;
                    });

                    valueRows.Add($"({string.Join(", ", paramNames)})");
                }

                sqlBuilder.Append(string.Join(", ", valueRows));
                string updateSet = string.Join(", ", _insertColumns.Select(c => $"{c} = EXCLUDED.{c}"));
                sqlBuilder.Append($" ON CONFLICT ({_idColumn}) DO UPDATE SET {updateSet} RETURNING {_idColumn};");

                var batchIds = await ExecuteIdReturningCommandAsync(sqlBuilder.ToString(), parameters, cancellationToken).ConfigureAwait(false);
                allReturnedIds.AddRange(batchIds);
            }

            // Update the IDs in the original entities
            for (int i = 0; i < entities.Count && i < allReturnedIds.Count; i++)
            {
                EntityReflectionCache<TEntity>.IdProperty?.SetValue(entities[i], allReturnedIds[i]);
            }

            return allReturnedIds;
        }

        /// <summary>
        /// Performs an optimized bulk soft delete with batched operations
        /// </summary>
        public async Task<IEnumerable<TKey>> BulkSoftDeleteAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default)
        {
            if (!ids.Any())
                return Enumerable.Empty<TKey>();

            var parameters = new Dictionary<string, object?> { { "@Ids", ids.ToArray() } };
            return await ExecuteIdReturningCommandAsync(_bulkSoftDeleteSql, parameters, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Performs an optimized bulk delete with batched operations
        /// </summary>
        public async Task<IEnumerable<TKey>> BulkDeleteAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default)
        {
            if (!ids.Any())
                return Enumerable.Empty<TKey>();

            var parameters = new Dictionary<string, object?> { { "@Ids", ids.ToArray() } };
            return await ExecuteIdReturningCommandAsync(_bulkHardDeleteSql, parameters, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Efficiently streams all non-deleted entities
        /// </summary>
        public async IAsyncEnumerable<TDomain> GetAllAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            using var command = PrepareCommand(_selectAllSql, new Dictionary<string, object?>());
            using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken).ConfigureAwait(false);

            var columns = GetReaderColumns(reader);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return _mapper.MapToModel(MapReaderToEntity(reader, columns));
            }
        }

        /// <summary>
        /// Efficiently streams paginated entities
        /// </summary>
        public async IAsyncEnumerable<TDomain> GetPaginatedAsync(
            int page,
            int pageSize,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            int offset = (page - 1) * pageSize;
            var parameters = new Dictionary<string, object?> {
                { "@PageSize", pageSize },
                { "@Offset", offset }
            };

            using var command = PrepareCommand(_selectPaginatedSql, parameters);
            using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken).ConfigureAwait(false);

            var columns = GetReaderColumns(reader);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return _mapper.MapToModel(MapReaderToEntity(reader, columns));
            }
        }

        /// <summary>
        /// Efficiently streams paginated entities with dynamic filtering and sorting
        /// </summary>
        public async IAsyncEnumerable<TDomain> GetPaginatedDynamicAsync(
            int page,
            int pageSize,
            string? additionalWhereClause = null,
            object? parametersObj = null,
            string? orderByClause = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // Generate the SQL dynamically based on parameters
            string sql = _sqlBuilder.SelectDynamicPaginated(additionalWhereClause, orderByClause);

            var parameters = CreateParametersFromObject(parametersObj) ?? new Dictionary<string, object?>();
            parameters.Add("@PageSize", pageSize);
            parameters.Add("@Offset", (page - 1) * pageSize);

            using var command = PrepareCommand(sql, parameters);
            using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken).ConfigureAwait(false);

            var columns = GetReaderColumns(reader);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return _mapper.MapToModel(MapReaderToEntity(reader, columns));
            }
        }

        /// <summary>
        /// Efficiently streams entities by their IDs
        /// </summary>
        public async IAsyncEnumerable<TDomain> GetByIdsAsync(
            IEnumerable<TKey> ids,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (!ids.Any())
                yield break;

            var parameters = new Dictionary<string, object?> { { "@Ids", ids.ToArray() } };

            using var command = PrepareCommand(_selectByIdsSql, parameters);
            using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken).ConfigureAwait(false);

            var columns = GetReaderColumns(reader);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return _mapper.MapToModel(MapReaderToEntity(reader, columns));
            }
        }

        /// <summary>
        /// Executes a custom query that returns a collection of scalar values
        /// </summary>
        public async Task<IEnumerable<TResult>> ExecuteQueryAsync<TResult>(
            string sql,
            object? parametersObj = null,
            CancellationToken cancellationToken = default)
        {
            var parameters = CreateParametersFromObject(parametersObj) ?? new Dictionary<string, object?>();
            var results = new List<TResult>();

            using var command = PrepareCommand(sql, parameters);
            using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            // Fast path for common types to avoid boxing/unboxing
            var resultType = typeof(TResult);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                object value = reader[0];

                if (value == DBNull.Value)
                {
                    results.Add(default!);
                    continue;
                }

                // Optimized type handling for common types
                if (resultType == typeof(int) && value is int intValue)
                    results.Add((TResult)(object)intValue);
                else if (resultType == typeof(long) && value is long longValue)
                    results.Add((TResult)(object)longValue);
                else if (resultType == typeof(decimal) && value is decimal decimalValue)
                    results.Add((TResult)(object)decimalValue);
                else if (resultType == typeof(string) && value is string stringValue)
                    results.Add((TResult)(object)stringValue);
                else if (resultType == typeof(DateTime) && value is DateTime dateTimeValue)
                    results.Add((TResult)(object)dateTimeValue);
                else if (resultType == typeof(bool) && value is bool boolValue)
                    results.Add((TResult)(object)boolValue);
                else
                    results.Add((TResult)Convert.ChangeType(value, resultType));
            }

            return results;
        }

        #endregion
    }
}