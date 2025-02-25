using ImmersiHome_API.Infrastructure.Persistence.Repositories.Common;
using ImmersiHome_API.Models.Domain;

namespace ImmersiHome_API.Services
{
    public interface IHouseService
    {
        IAsyncEnumerable<HouseModel> GetRecentlyListedHousesAsync(int count, CancellationToken cancellationToken = default);
        IAsyncEnumerable<HouseModel> GetHousesByLocationAsync(decimal latitude, decimal longitude, decimal radiusInKm, CancellationToken cancellationToken = default);
        Task<HouseModel> AddHouseAsync(HouseModel house, CancellationToken cancellationToken = default);
        Task<HouseModel?> GetHouseByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<bool> DeleteHouseAsync(int id, CancellationToken cancellationToken = default);
    }
}
