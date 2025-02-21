using ImmersiHome_API.Models.Domain.Common;
using ImmersiHome_API.Models.Entities.Common;
using System.Linq.Expressions;

namespace ImmersiHome_API.Persistence.Repositories.Common
{
    public interface IGenericRepository<TDomain, TKey>
        where TDomain : IGenericModel<TKey>
        where TKey : struct, IEquatable<TKey>
    {
        Task<TDomain> AddAsync(TDomain model, CancellationToken cancellationToken = default);
        Task<TDomain> UpdateAsync(TDomain model, CancellationToken cancellationToken = default);
        Task<TDomain> UpsertAsync(TDomain model, CancellationToken cancellationToken = default);
        Task<bool> SoftDeleteAsync(TKey id, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(TKey id, CancellationToken cancellationToken = default);
        Task<IEnumerable<TKey>> BulkUpsertAsync(IEnumerable<TDomain> models, CancellationToken cancellationToken = default);
        Task<IEnumerable<TKey>> BulkSoftDeleteAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default);
        Task<IEnumerable<TKey>> BulkDeleteAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default);
        Task<TDomain?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default);
        IAsyncEnumerable<TDomain> GetAllAsync(CancellationToken cancellationToken = default);
        IAsyncEnumerable<TDomain> GetPaginatedAsync(
            int page,
            int pageSize,
            Expression<Func<object, bool>>? filter = null,
            Expression<Func<object, object>>? orderBy = null,
            bool descending = false,
            CancellationToken cancellationToken = default);
        IAsyncEnumerable<TDomain> GetPaginatedDynamicAsync(
            int page,
            int pageSize,
            string? whereClause = null,
            object? parameters = null,
            string? orderByClause = null,
            CancellationToken cancellationToken = default);
        IAsyncEnumerable<TDomain> GetByIdsAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default);
        IQueryable<TDomain> Query();
        Task<bool> ExistsAsync(TKey id, CancellationToken cancellationToken = default);
        Task<int> CountAsync(Expression<Func<object, bool>>? predicate = null, CancellationToken cancellationToken = default);
        Task<IEnumerable<TResult>> ExecuteQueryAsync<TResult>(string sql, object? parameters = null, CancellationToken cancellationToken = default);
    }
}
