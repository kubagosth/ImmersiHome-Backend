using BoligPletten.Application.Services;
using BoligPletten.Domain.Models;
using Microsoft.AspNetCore.Mvc;

namespace ImmersiHome_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HousesController : ControllerBase
    {
        private readonly IHouseService _houseService;

        public HousesController(IHouseService houseService)
        {
            _houseService = houseService;
        }

        [HttpGet("recent")]
        public async Task<ActionResult<IEnumerable<HouseModel>>> GetRecentHouses(
            [FromQuery] int count = 10,
            CancellationToken cancellationToken = default)
        {
            var houses = new List<HouseModel>();

            // Consume the IAsyncEnumerable
            await foreach (var house in _houseService.GetRecentlyListedHousesAsync(count, cancellationToken)
                .ConfigureAwait(false))
            {
                houses.Add(house);
            }

            return Ok(houses);
        }

        [HttpGet("nearby")]
        public async Task<ActionResult<IEnumerable<HouseModel>>> GetNearbyHouses(
            [FromQuery] decimal latitude,
            [FromQuery] decimal longitude,
            [FromQuery] decimal radiusInKm = 10,
            CancellationToken cancellationToken = default)
        {
            var houses = new List<HouseModel>();

            await foreach (var house in _houseService.GetHousesByLocationAsync(
                latitude, longitude, radiusInKm, cancellationToken).ConfigureAwait(false))
            {
                houses.Add(house);
            }

            return Ok(houses);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<HouseModel>> GetHouse(
            int id,
            CancellationToken cancellationToken = default)
        {
            var house = await _houseService.GetHouseByIdAsync(id, cancellationToken).ConfigureAwait(false);

            if (house == null)
            {
                return NotFound();
            }

            return Ok(house);
        }

        [HttpPost]
        public async Task<ActionResult<HouseModel>> CreateHouse(
            [FromBody] HouseModel house,
            CancellationToken cancellationToken = default)
        {
            var createdHouse = await _houseService.AddHouseAsync(house, cancellationToken).ConfigureAwait(false);
            return CreatedAtAction(nameof(GetHouse), new { id = createdHouse.Id }, createdHouse);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteHouse(
            int id,
            CancellationToken cancellationToken = default)
        {
            var result = await _houseService.DeleteHouseAsync(id, cancellationToken).ConfigureAwait(false);

            if (!result)
            {
                return NotFound();
            }

            return NoContent();
        }
    }
}
