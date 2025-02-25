using ImmersiHome_API.Infrastructure.Persistence.Repositories.Common;
using ImmersiHome_API.Models.Domain;

namespace ImmersiHome_API.Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// Interface for house-specific repository operations
    /// </summary>
    public interface IHouseRepository : IGenericRepository<HouseModel, int>
    {
        /// <summary>
        /// Gets the most recently listed houses
        /// </summary>
        /// <param name="count">Number of houses to return</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Collection of recently listed houses</returns>
        IAsyncEnumerable<HouseModel> GetRecentlyListedHousesAsync(
            int count,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets houses within a geographical radius
        /// </summary>
        /// <param name="latitude">Center latitude</param>
        /// <param name="longitude">Center longitude</param>
        /// <param name="radiusInKm">Radius in kilometers</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Collection of houses within the specified radius</returns>
        IAsyncEnumerable<HouseModel> GetHousesByLocationAsync(
            decimal latitude,
            decimal longitude,
            decimal radiusInKm,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets houses with specific amenities
        /// </summary>
        IAsyncEnumerable<HouseModel> GetHousesByAmenitiesAsync(
            IEnumerable<string> amenities,
            int page = 1,
            int pageSize = 20,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets houses within a price range
        /// </summary>
        IAsyncEnumerable<HouseModel> GetHousesByPriceRangeAsync(
            decimal minPrice,
            decimal maxPrice,
            int page = 1,
            int pageSize = 20,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets newly added houses (listed within the last N days)
        /// </summary>
        IAsyncEnumerable<HouseModel> GetNewlyAddedHousesAsync(
            int days = 7,
            int limit = 10,
            CancellationToken cancellationToken = default);
    }
}
