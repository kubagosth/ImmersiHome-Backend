using ImmersiHome_API.Models.Domain;
using ImmersiHome_API.Services;
using Microsoft.AspNetCore.Mvc;

namespace ImmersiHome_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HousesController : ControllerBase
    {
        private readonly IHouseService _houseService;
        private readonly ILogger<HousesController> _logger;

        public HousesController(IHouseService houseService, ILogger<HousesController> logger)
        {
            _houseService = houseService ?? throw new ArgumentNullException(nameof(houseService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<HouseModel>> GetById(int id)
        {
            var house = await _houseService.GetByIdAsync(id);
            if (house == null)
            {
                _logger.LogWarning("House with id {Id} not found", id);
                return NotFound();
            }
            return Ok(house);
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<HouseModel>>> GetAll()
        {
            var houses = await _houseService.GetAllAsync();
            return Ok(houses);
        }

        [HttpPost]
        public async Task<ActionResult<HouseModel>> Create([FromBody] HouseModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var createdHouse = await _houseService.AddAsync(model);
            return CreatedAtAction(nameof(GetById), new { id = createdHouse.Id }, createdHouse);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<HouseModel>> Update(int id, [FromBody] HouseModel model)
        {
            if (id != model.Id)
            {
                return BadRequest("The id in the URL must match the id in the body.");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var updatedHouse = await _houseService.UpdateAsync(model);
            return Ok(updatedHouse);
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(int id)
        {
            var success = await _houseService.DeleteAsync(id);
            if (!success)
            {
                return NotFound();
            }
            return NoContent();
        }

        [HttpGet("recent")]
        public async Task<ActionResult<IEnumerable<HouseModel>>> GetRecent([FromQuery] int count = 10)
        {
            var houses = await _houseService.GetRecentlyListedHousesAsync(count);
            return Ok(houses);
        }

        [HttpGet("bylocation")]
        public async Task<ActionResult<IEnumerable<HouseModel>>> GetByLocation(
            [FromQuery] decimal latitude,
            [FromQuery] decimal longitude,
            [FromQuery] decimal radiusInKm)
        {
            var houses = await _houseService.GetHousesByLocationAsync(latitude, longitude, radiusInKm);
            return Ok(houses);
        }
    }
}
