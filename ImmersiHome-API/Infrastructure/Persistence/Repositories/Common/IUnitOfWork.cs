namespace ImmersiHome_API.Infrastructure.Persistence.Repositories.Common
{
    public interface IUnitOfWork : IDisposable
    {
        // Repository accessor - uses lazy initialization
        IHouseRepository Houses { get; }

        Task CommitAsync(CancellationToken cancellationToken = default);
        Task RollbackAsync(CancellationToken cancellationToken = default);
    }
}
