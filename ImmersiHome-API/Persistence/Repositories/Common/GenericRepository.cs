using Dapper;
using ImmersiHome_API.Models.Domain.Common;
using ImmersiHome_API.Models.Entities.Common;
using ImmersiHome_API.Persistence.Mappers;
using Microsoft.Extensions.Caching.Memory;
using System.Data;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace ImmersiHome_API.Persistence.Repositories.Common
{
    public class GenericRepository<TDomain, TEntity, TKey> : IGenericRepository<TDomain, TKey>
        where TDomain : IGenericModel<TKey>, new()
        where TEntity : IGenericEntity<TKey>, new()
        where TKey : struct, IEquatable<TKey>
    {
        protected readonly IDbConnection _connection;
        protected readonly IDbTransaction _transaction;
        protected readonly IMemoryCache _cache;
        protected readonly IGenericMapper<TDomain, TEntity, TKey> _mapper;
        private readonly MemoryCacheEntryOptions _cacheOptions;

        public GenericRepository(IDbConnection connection, IDbTransaction transaction, IMemoryCache cache, IGenericMapper<TDomain, TEntity, TKey> mapper)
        {
            _connection = connection;
            _transaction = transaction;
            _cache = cache;
            _mapper = mapper;
            _cacheOptions = new MemoryCacheEntryOptions().SetSlidingExpiration(TimeSpan.FromMinutes(15));
        }

        private string GetCacheKey(object id) => $"{typeof(TEntity).Name}_{id}";

        private async Task<TEntity?> GetEntityByIdAsync(TKey id, CancellationToken cancellationToken = default)
        {
            var sql = $"SELECT * FROM {typeof(TEntity).Name} WHERE Id = @Id AND IsDeleted = FALSE;";
            return await _connection.QuerySingleOrDefaultAsync<TEntity>(sql, new { Id = id }, _transaction);
        }

        public async Task<TDomain> AddAsync(TDomain model, CancellationToken cancellationToken = default)
        {
            var entity = _mapper.MapToEntity(model);
            var columns = GetColumns(entity, includeId: false);
            var parameters = string.Join(", ", columns.Select(c => "@" + c));
            var sql = $"INSERT INTO {typeof(TEntity).Name} ({string.Join(", ", columns)}) VALUES ({parameters}) RETURNING Id;";
            var id = await _connection.ExecuteScalarAsync<TKey>(sql, entity, _transaction);
            entity.Id = id;
            var result = _mapper.MapToModel(entity);
            _cache.Set(GetCacheKey(id), result, _cacheOptions);
            return result;
        }

        public async Task<TDomain> UpdateAsync(TDomain model, CancellationToken cancellationToken = default)
        {
            var existing = await GetEntityByIdAsync(model.Id, cancellationToken);
            if (existing == null)
                throw new Exception($"Entity with id {model.Id} not found.");
            var updated = _mapper.MapToEntity(model);

            // Preserve existing model values
            updated.CreatedUtc = existing.CreatedUtc;
            updated.IsDeleted = existing.IsDeleted;

            // Mark the model as updated
            updated.ModelUpdated();

            var columns = GetColumns(updated, includeId: false);
            var setClause = string.Join(", ", columns.Select(c => $"{c} = @{c}"));
            var sql = $"UPDATE {typeof(TEntity).Name} SET {setClause} WHERE Id = @Id RETURNING Id;";
            var id = await _connection.ExecuteScalarAsync<TKey>(sql, updated, _transaction);
            updated.Id = id;
            _cache.Remove(GetCacheKey(id));
            var result = _mapper.MapToModel(updated);
            _cache.Set(GetCacheKey(id), result, _cacheOptions);
            return result;
        }

        public async Task<TDomain> UpsertAsync(TDomain model, CancellationToken cancellationToken = default)
        {
            // For upsert we rely on the database to determine whether to insert or update.
            var entity = _mapper.MapToEntity(model);
            var columns = GetColumns(entity, includeId: false);
            var parameters = string.Join(", ", columns.Select(c => "@" + c));
            var updateSet = string.Join(", ", columns.Select(c => $"{c} = EXCLUDED.{c}"));
            var sql = $"INSERT INTO {typeof(TEntity).Name} ({string.Join(", ", columns)}) VALUES ({parameters}) " +
                      $"ON CONFLICT (Id) DO UPDATE SET {updateSet} RETURNING Id;";
            var id = await _connection.ExecuteScalarAsync<TKey>(sql, entity, _transaction);
            entity.Id = id;
            var result = _mapper.MapToModel(entity);
            _cache.Set(GetCacheKey(id), result, _cacheOptions);
            return result;
        }

        public async Task<TDomain?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default)
        {
            var cacheKey = GetCacheKey(id);
            if (_cache.TryGetValue<TDomain>(cacheKey, out var cached))
                return cached;
            var sql = $"SELECT * FROM {typeof(TEntity).Name} WHERE Id = @Id AND IsDeleted = FALSE;";
            var entity = await _connection.QuerySingleOrDefaultAsync<TEntity>(sql, new { Id = id }, _transaction);
            if (entity == null)
                return default;
            var result = _mapper.MapToModel(entity);
            _cache.Set(cacheKey, result, _cacheOptions);
            return result;
        }

        public async Task<bool> ExistsAsync(TKey id, CancellationToken cancellationToken = default)
        {
            var sql = $"SELECT COUNT(1) FROM {typeof(TEntity).Name} WHERE Id = @Id AND IsDeleted = FALSE;";
            var count = await _connection.ExecuteScalarAsync<int>(sql, new { Id = id }, _transaction);
            return count > 0;
        }

        public async Task<int> CountAsync(Expression<Func<object, bool>>? predicate = null, CancellationToken cancellationToken = default)
        {
            if (predicate != null)
                throw new NotSupportedException("Dynamic predicate expressions are not supported; please build the query manually.");
            var sql = $"SELECT COUNT(1) FROM {typeof(TEntity).Name} WHERE IsDeleted = FALSE;";
            return await _connection.ExecuteScalarAsync<int>(sql, transaction: _transaction);
        }

        public async Task<bool> SoftDeleteAsync(TKey id, CancellationToken cancellationToken = default)
        {
            var entity = await GetEntityByIdAsync(id, cancellationToken);
            if (entity == null)
                throw new Exception($"Entity with id {id} not found.");
            entity.MarkAsDeleted();
            var columns = GetColumns(entity, includeId: false);
            var setClause = string.Join(", ", columns.Select(c => $"{c} = @{c}"));
            var sql = $"UPDATE {typeof(TEntity).Name} SET {setClause} WHERE Id = @Id RETURNING Id;";
            var affectedId = await _connection.ExecuteScalarAsync<TKey>(sql, entity, _transaction);
            _cache.Remove(GetCacheKey(id));
            return !EqualityComparer<TKey>.Default.Equals(affectedId, default);
        }

        public async Task<bool> DeleteAsync(TKey id, CancellationToken cancellationToken = default)
        {
            var sql = $"DELETE FROM {typeof(TEntity).Name} WHERE Id = @Id RETURNING Id;";
            var affectedId = await _connection.ExecuteScalarAsync<TKey>(sql, new { Id = id }, _transaction);
            _cache.Remove(GetCacheKey(id));
            return !EqualityComparer<TKey>.Default.Equals(affectedId, default);
        }

        public async Task<IEnumerable<TKey>> BulkUpsertAsync(IEnumerable<TDomain> models, CancellationToken cancellationToken = default)
        {
            if (!models.Any())
                return Enumerable.Empty<TKey>();
            var entities = models.Select(m => _mapper.MapToEntity(m)).ToList();
            var columns = GetColumns(entities.First(), includeId: false).ToList();
            var sqlBuilder = new StringBuilder();
            var parameters = new DynamicParameters();
            sqlBuilder.Append($"INSERT INTO {typeof(TEntity).Name} ({string.Join(", ", columns)}) VALUES ");
            var valueRows = new List<string>();
            int rowIndex = 0;
            foreach (var entity in entities)
            {
                var paramNames = new List<string>();
                foreach (var col in columns)
                {
                    var paramName = $"@{col}_{rowIndex}";
                    paramNames.Add(paramName);
                    var prop = entity.GetType().GetProperty(col);
                    parameters.Add(paramName, prop?.GetValue(entity));
                }
                valueRows.Add($"({string.Join(", ", paramNames)})");
                rowIndex++;
            }
            sqlBuilder.Append(string.Join(", ", valueRows));
            var updateSet = string.Join(", ", columns.Select(c => $"{c} = EXCLUDED.{c}"));
            sqlBuilder.Append($" ON CONFLICT (Id) DO UPDATE SET {updateSet} RETURNING Id;");
            var sql = sqlBuilder.ToString();
            var ids = await _connection.QueryAsync<TKey>(sql, parameters, _transaction);
            foreach (var entity in entities)
                _cache.Set(GetCacheKey(entity.Id), _mapper.MapToModel(entity), _cacheOptions);
            return ids;
        }

        public async Task<IEnumerable<TKey>> BulkSoftDeleteAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default)
        {
            var sql = $"UPDATE {typeof(TEntity).Name} SET IsDeleted = TRUE WHERE Id = ANY(@Ids) RETURNING Id;";
            var deletedIds = await _connection.QueryAsync<TKey>(sql, new { Ids = ids.ToArray() }, _transaction);
            foreach (var id in deletedIds)
                _cache.Remove(GetCacheKey(id));
            return deletedIds;
        }

        public async Task<IEnumerable<TKey>> BulkDeleteAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default)
        {
            var sql = $"DELETE FROM {typeof(TEntity).Name} WHERE Id = ANY(@Ids) RETURNING Id;";
            var deletedIds = await _connection.QueryAsync<TKey>(sql, new { Ids = ids.ToArray() }, _transaction);
            foreach (var id in deletedIds)
                _cache.Remove(GetCacheKey(id));
            return deletedIds;
        }

        public async IAsyncEnumerable<TDomain> GetAllAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var sql = $"SELECT * FROM {typeof(TEntity).Name} WHERE IsDeleted = FALSE;";
            var result = await _connection.QueryAsync<TEntity>(sql, transaction: _transaction);
            foreach (var entity in result)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return _mapper.MapToModel(entity);
            }
        }

        public async IAsyncEnumerable<TDomain> GetPaginatedAsync(
            int page,
            int pageSize,
            Expression<Func<object, bool>>? filter = null,
            Expression<Func<object, object>>? orderBy = null,
            bool descending = false,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (filter != null || orderBy != null)
                throw new NotSupportedException("Dynamic expressions are not supported. Use GetPaginatedDynamicAsync instead.");
            var offset = (page - 1) * pageSize;
            var sql = $"SELECT * FROM {typeof(TEntity).Name} WHERE IsDeleted = FALSE LIMIT @PageSize OFFSET @Offset;";
            var parameters = new DynamicParameters();
            parameters.Add("PageSize", pageSize);
            parameters.Add("Offset", offset);
            var result = await _connection.QueryAsync<TEntity>(sql, parameters, _transaction);
            foreach (var entity in result)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return _mapper.MapToModel(entity);
            }
        }

        public async IAsyncEnumerable<TDomain> GetPaginatedDynamicAsync(
            int page,
            int pageSize,
            string? whereClause = null,
            object? parameters = null,
            string? orderByClause = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var sqlBuilder = new StringBuilder();
            sqlBuilder.Append($"SELECT * FROM {typeof(TEntity).Name}");
            if (!string.IsNullOrWhiteSpace(whereClause))
                sqlBuilder.Append(" WHERE " + whereClause);
            else
                sqlBuilder.Append(" WHERE IsDeleted = FALSE");
            if (!string.IsNullOrWhiteSpace(orderByClause))
                sqlBuilder.Append(" ORDER BY " + orderByClause);
            sqlBuilder.Append(" LIMIT @PageSize OFFSET @Offset;");
            var dynamicParams = new DynamicParameters(parameters);
            dynamicParams.Add("PageSize", pageSize);
            dynamicParams.Add("Offset", (page - 1) * pageSize);
            var result = await _connection.QueryAsync<TEntity>(sqlBuilder.ToString(), dynamicParams, _transaction);
            foreach (var entity in result)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return _mapper.MapToModel(entity);
            }
        }

        public async IAsyncEnumerable<TDomain> GetByIdsAsync(IEnumerable<TKey> ids, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var sql = $"SELECT * FROM {typeof(TEntity).Name} WHERE Id = ANY(@Ids) AND IsDeleted = FALSE;";
            var result = await _connection.QueryAsync<TEntity>(sql, new { Ids = ids.ToArray() }, _transaction);
            foreach (var entity in result)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return _mapper.MapToModel(entity);
            }
        }

        public IQueryable<TDomain> Query()
        {
            var sql = $"SELECT * FROM {typeof(TEntity).Name} WHERE IsDeleted = FALSE;";
            var list = _connection.Query<TEntity>(sql, transaction: _transaction).ToList();
            return list.Select(e => _mapper.MapToModel(e)).AsQueryable();
        }

        public async Task<IEnumerable<TResult>> ExecuteQueryAsync<TResult>(string sql, object? parameters = null, CancellationToken cancellationToken = default)
        {
            return await _connection.QueryAsync<TResult>(sql, parameters, _transaction);
        }

        private IEnumerable<string> GetColumns(TEntity entity, bool includeId)
        {
            var props = typeof(TEntity).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                                       .Where(p => p.CanRead && p.CanWrite);
            if (!includeId)
                props = props.Where(p => !p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase));
            props = props.Where(p => !p.Name.Equals("IsDeleted", StringComparison.OrdinalIgnoreCase));
            return props.Select(p => p.Name);
        }
    }
}
