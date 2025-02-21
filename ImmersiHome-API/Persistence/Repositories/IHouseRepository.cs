using ImmersiHome_API.Models.Domain;
using ImmersiHome_API.Persistence.Repositories.Common;

namespace ImmersiHome_API.Persistence.Repositories
{
    public interface IHouseRepository : IGenericRepository<HouseModel, int>
    {
        IAsyncEnumerable<HouseModel> GetRecentlyListedHousesAsync(int count, CancellationToken cancellationToken = default);
        IAsyncEnumerable<HouseModel> GetHousesByLocationAsync(decimal latitude, decimal longitude, decimal radiusInKm, CancellationToken cancellationToken = default);
    }
}
