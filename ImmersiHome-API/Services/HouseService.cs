using ImmersiHome_API.Models.Domain;
using ImmersiHome_API.Models.Entities;
using ImmersiHome_API.Persistence.Repositories;
using ImmersiHome_API.Persistence.Repositories.Common;
using ImmersiHome_API.Services.Common;

namespace ImmersiHome_API.Services
{
    public class HouseService : GenericService<HouseModel, HouseEntity, int>, IHouseService
    {
        private IHouseRepository HouseRepository => Repository as IHouseRepository
            ?? throw new InvalidOperationException("Repository is not of type IHouseRepository");

        public HouseService(IUnitOfWork unitOfWork, ILogger<HouseService> logger)
            : base(unitOfWork, logger)
        {
        }

        public async Task<IEnumerable<HouseModel>> GetRecentlyListedHousesAsync(int count, CancellationToken cancellationToken = default)
        {
            try
            {
                Logger.LogInformation("Retrieving {Count} recently listed houses", count);
                var houses = new List<HouseModel>();
                await foreach (var house in HouseRepository.GetRecentlyListedHousesAsync(count, cancellationToken))
                {
                    houses.Add(house);
                }
                Logger.LogInformation("{Count} houses retrieved", houses.Count);
                return houses;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error retrieving recently listed houses");
                throw new HouseServiceException("Error retrieving recently listed houses", ex);
            }
        }

        public async Task<IEnumerable<HouseModel>> GetHousesByLocationAsync(decimal latitude, decimal longitude, decimal radiusInKm, CancellationToken cancellationToken = default)
        {
            try
            {
                Logger.LogInformation("Retrieving houses by location: lat {lat}, lon {lon}, radius {radius}", latitude, longitude, radiusInKm);
                var houses = new List<HouseModel>();
                await foreach (var house in HouseRepository.GetHousesByLocationAsync(latitude, longitude, radiusInKm, cancellationToken))
                {
                    houses.Add(house);
                }
                Logger.LogInformation("{Count} houses retrieved by location", houses.Count);
                return houses;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error retrieving houses by location");
                throw new HouseServiceException("Error retrieving houses by location", ex);
            }
        }
    }
}
