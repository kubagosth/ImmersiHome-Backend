using ImmersiHome_API.Models.Domain;
using ImmersiHome_API.Persistence.Repositories;
using ImmersiHome_API.Persistence.Repositories.Common;

namespace ImmersiHome_API.Services
{
    public class HouseService
    {
        public HouseService(IUnitOfWork unitOfWork, ILogger<HouseService> logger)
        {

        }

        private readonly ILogger<HouseService> _logger;

        public async Task<IEnumerable<HouseModel>> GetRecentlyListedHousesAsync(int count, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Retrieving {Count} recently listed houses", count);
                var houses = new List<HouseModel>();
                await foreach (var house in HouseRepository.GetRecentlyListedHousesAsync(count, cancellationToken))
                {
                    houses.Add(house);
                }
                _logger.LogInformation("{Count} houses retrieved", houses.Count);
                return houses;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving recently listed houses");
                throw new HouseServiceException("Error retrieving recently listed houses", ex);
            }
        }

        public async Task<IEnumerable<HouseModel>> GetHousesByLocationAsync(decimal latitude, decimal longitude, decimal radiusInKm, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Retrieving houses by location: lat {lat}, lon {lon}, radius {radius}", latitude, longitude, radiusInKm);
                var houses = new List<HouseModel>();
                await foreach (var house in HouseRepository.GetHousesByLocationAsync(latitude, longitude, radiusInKm, cancellationToken))
                {
                    houses.Add(house);
                }
                _logger.LogInformation("{Count} houses retrieved by location", houses.Count);
                return houses;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving houses by location");
                throw new HouseServiceException("Error retrieving houses by location", ex);
            }
        }
    }
}
