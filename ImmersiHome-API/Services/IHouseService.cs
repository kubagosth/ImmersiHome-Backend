using ImmersiHome_API.Models.Domain;
using ImmersiHome_API.Services.Common;

namespace ImmersiHome_API.Services
{
    public interface IHouseService : IGenericService<HouseModel, int>
    {
        Task<IEnumerable<HouseModel>> GetRecentlyListedHousesAsync(int count, CancellationToken cancellationToken = default);
        Task<IEnumerable<HouseModel>> GetHousesByLocationAsync(decimal latitude, decimal longitude, decimal radiusInKm, CancellationToken cancellationToken = default);
    }
}
