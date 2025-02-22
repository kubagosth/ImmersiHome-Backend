using ImmersiHome_API.Models.Domain;

namespace ImmersiHome_API.Services
{
    public interface IHouseService
    {
        Task<IEnumerable<HouseModel>> GetRecentlyListedHousesAsync(int count, CancellationToken cancellationToken = default);
        Task<IEnumerable<HouseModel>> GetHousesByLocationAsync(decimal latitude, decimal longitude, decimal radiusInKm, CancellationToken cancellationToken = default);
    }
}
