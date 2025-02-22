using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using ImmersiHome_API.Models.Domain.Common;

namespace ImmersiHome_API.Persistence.Repositories.Common
{
    public interface IGenericRepository<TDomain, TKey>
        where TDomain : IGenericModel<TKey>
        where TKey : struct, IEquatable<TKey>
    {
        Task<TDomain> AddAsync(TDomain model, CancellationToken cancellationToken = default);
        Task<TDomain> UpdateAsync(TDomain model, CancellationToken cancellationToken = default);
        Task<TDomain> UpsertAsync(TDomain model, CancellationToken cancellationToken = default);
        Task<TDomain?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default);
        Task<bool> ExistsAsync(TKey id, CancellationToken cancellationToken = default);
        Task<int> CountAsync(CancellationToken cancellationToken = default);
        Task<bool> SoftDeleteAsync(TKey id, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(TKey id, CancellationToken cancellationToken = default);
        Task<IEnumerable<TKey>> BulkUpsertAsync(IEnumerable<TDomain> models, CancellationToken cancellationToken = default);
        Task<IEnumerable<TKey>> BulkSoftDeleteAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default);
        Task<IEnumerable<TKey>> BulkDeleteAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default);
        IAsyncEnumerable<TDomain> GetAllAsync(CancellationToken cancellationToken = default);
        IAsyncEnumerable<TDomain> GetPaginatedAsync(int page, int pageSize, CancellationToken cancellationToken = default);
        IAsyncEnumerable<TDomain> GetPaginatedDynamicAsync(
            int page,
            int pageSize,
            string? additionalWhereClause = null,
            object? parametersObj = null,
            string? orderByClause = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default);
        IAsyncEnumerable<TDomain> GetByIdsAsync(IEnumerable<TKey> ids, CancellationToken cancellationToken = default);
        Task<IEnumerable<TResult>> ExecuteQueryAsync<TResult>(string sql, object? parametersObj = null, CancellationToken cancellationToken = default);
    }
}
