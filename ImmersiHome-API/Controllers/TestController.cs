using Microsoft.AspNetCore.Mvc;

namespace ImmersiHome_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestController : ControllerBase
    {
        private static readonly Random _random = new();

        /// <summary>
        /// Returns a random number between 1 and 100.
        /// </summary>
        [HttpGet("random-number")]
        public IActionResult GetRandomNumber()
        {
            int randomNumber = _random.Next(1, 101);
            return Ok(new { number = randomNumber });
        }

        /// <summary>
        /// Returns a simple hello message.
        /// </summary>
        [HttpGet("hello")]
        public IActionResult SayHello()
        {
            return Ok(new { message = "Hello from the test controller!" });
        }

        /// <summary>
        /// Returns a sample test object.
        /// </summary>
        [HttpGet("test-object")]
        public IActionResult GetTestObject()
        {
            var testObject = new
            {
                Id = _random.Next(1, 1000),
                Name = "Test Item",
                Description = "This is a test object.",
                Timestamp = DateTime.UtcNow
            };

            return Ok(testObject);
        }
    }
}
