using ImmersiHome_API.Models.Domain.Common;

namespace ImmersiHome_API.Services.Common
{
    /// <summary>
    /// Defines CRUD operations for domain models.
    /// </summary>
    /// <typeparam name="TDomain">The domain model type.</typeparam>
    /// <typeparam name="TKey">The type of the primary key.</typeparam>
    public interface IGenericService<TDomain, TKey>
        where TDomain : IGenericModel<TKey>, new()
        where TKey : struct, IEquatable<TKey>
    {
        Task<TDomain> AddAsync(TDomain model, CancellationToken cancellationToken = default);
        Task<TDomain> UpdateAsync(TDomain model, CancellationToken cancellationToken = default);
        Task<TDomain> UpsertAsync(TDomain model, CancellationToken cancellationToken = default);
        Task<bool> SoftDeleteAsync(TKey id, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(TKey id, CancellationToken cancellationToken = default);
        Task<TDomain?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default);
        Task<IEnumerable<TDomain>> GetAllAsync(CancellationToken cancellationToken = default);
    }
}
