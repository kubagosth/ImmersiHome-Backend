namespace ImmersiHome_API.Infrastructure.Persistence.Repositories.Common
{
    public interface IRepository
    {
        /// <summary>
        /// Called by the UnitOfWork during commit to flush changes.
        /// This can be used for tracking
        /// If your repository does not track pending changes, this can be a no-op.
        /// </summary>
        void Submit();
    }
}