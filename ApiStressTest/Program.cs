using ApiStressTest;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ImmersiHome.StressTest
{
    class Program
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private static readonly Random _random = new Random();
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        // API configuration
        private static string _baseUrl = "https://milkdrift.com/api"; // Default URL
        private static int _concurrentUsers = 10000;
        private static int _runTimeSeconds = 60;
        private static int _rampUpTimeSeconds = 5;
        private static bool _verbose = false;

        // Statistics
        private static long _totalRequests = 0;
        private static long _successfulRequests = 0;
        private static long _failedRequests = 0;
        private static readonly Dictionary<string, EndpointStatistics> _endpointStats = new();
        private static readonly object _lockObject = new object();

        static async Task Main(string[] args)
        {
            Console.WriteLine("ImmersiHome API Stress Test Tool");
            Console.WriteLine("===============================");

            ParseArguments(args);

            await RunStressTest();

            DisplayResults();

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }

        static void ParseArguments(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLower())
                {
                    case "-url":
                    case "--url":
                        if (i + 1 < args.Length)
                            _baseUrl = args[++i];
                        break;
                    case "-u":
                    case "--users":
                        if (i + 1 < args.Length && int.TryParse(args[++i], out int users))
                            _concurrentUsers = users;
                        break;
                    case "-t":
                    case "--time":
                        if (i + 1 < args.Length && int.TryParse(args[++i], out int time))
                            _runTimeSeconds = time;
                        break;
                    case "-r":
                    case "--ramp":
                        if (i + 1 < args.Length && int.TryParse(args[++i], out int ramp))
                            _rampUpTimeSeconds = ramp;
                        break;
                    case "-v":
                    case "--verbose":
                        _verbose = true;
                        break;
                    case "-h":
                    case "--help":
                        ShowHelp();
                        Environment.Exit(0);
                        break;
                }
            }

            Console.WriteLine($"Base URL: {_baseUrl}");
            Console.WriteLine($"Concurrent users: {_concurrentUsers}");
            Console.WriteLine($"Run time: {_runTimeSeconds} seconds");
            Console.WriteLine($"Ramp up time: {_rampUpTimeSeconds} seconds");
            Console.WriteLine($"Verbose mode: {_verbose}");
            Console.WriteLine();
        }

        static void ShowHelp()
        {
            Console.WriteLine("\nUsage: ImmersiHome.StressTest [options]");
            Console.WriteLine("\nOptions:");
            Console.WriteLine("  -url, --url <url>       API base URL (default: http://localhost:5000/api)");
            Console.WriteLine("  -u, --users <number>    Number of concurrent users (default: 100)");
            Console.WriteLine("  -t, --time <seconds>    Test duration in seconds (default: 60)");
            Console.WriteLine("  -r, --ramp <seconds>    Ramp-up time in seconds (default: 5)");
            Console.WriteLine("  -v, --verbose           Enable verbose output");
            Console.WriteLine("  -h, --help              Show help information");
        }

        static async Task RunStressTest()
        {
            Console.WriteLine("\nStarting stress test...");

            var cancellationTokenSource = new CancellationTokenSource();
            var token = cancellationTokenSource.Token;

            // Configure HttpClient
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "ImmersiHome-StressTest/1.0");

            var tasks = new List<Task>();
            var startTime = DateTime.UtcNow;

            // Start user tasks with ramp-up
            Console.WriteLine("Ramping up users...");
            for (int i = 0; i < _concurrentUsers; i++)
            {
                var userTask = SimulateUserActivity(i, token);
                tasks.Add(userTask);

                if (_rampUpTimeSeconds > 0 && i < _concurrentUsers - 1)
                {
                    var delayMs = (_rampUpTimeSeconds * 1000) / _concurrentUsers;
                    await Task.Delay(delayMs > 0 ? delayMs : 1);
                }
            }

            Console.WriteLine($"All {_concurrentUsers} users are now active");
            Console.WriteLine($"Test running for {_runTimeSeconds} seconds...");

            // Wait for the specified duration
            await Task.Delay(TimeSpan.FromSeconds(_runTimeSeconds));

            // Stop the test
            cancellationTokenSource.Cancel();

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation token is used
            }

            var endTime = DateTime.UtcNow;
            var elapsedSeconds = (endTime - startTime).TotalSeconds;

            Console.WriteLine("\nTest completed!");
            Console.WriteLine($"Actual test duration: {elapsedSeconds:F2} seconds");
        }

        static async Task SimulateUserActivity(int userId, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Randomly select an API endpoint to call
                    var endpoint = GetRandomEndpoint();

                    if (_verbose)
                        Console.WriteLine($"User {userId} calling {endpoint.Name}");

                    var stopwatch = Stopwatch.StartNew();
                    var response = await endpoint.Action(cancellationToken);
                    stopwatch.Stop();

                    // Update statistics
                    UpdateStatistics(endpoint.Name, response.IsSuccessStatusCode, stopwatch.ElapsedMilliseconds);

                    if (_verbose)
                    {
                        string statusInfo = response.IsSuccessStatusCode
                            ? "success"
                            : $"failed ({response.StatusCode})";
                        Console.WriteLine($"User {userId} completed {endpoint.Name}: {statusInfo} in {stopwatch.ElapsedMilliseconds}ms");
                    }

                    // Random delay between requests to simulate user think time (50-500ms)
                    await Task.Delay(_random.Next(50, 501), cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Test was canceled, exit gracefully
                    break;
                }
                catch (Exception ex)
                {
                    lock (_lockObject)
                    {
                        _totalRequests++;
                        _failedRequests++;
                    }

                    if (_verbose)
                        Console.WriteLine($"User {userId} error: {ex.Message}");
                }
            }
        }

        static (string Name, Func<CancellationToken, Task<HttpResponseMessage>> Action) GetRandomEndpoint()
        {
            // Define the API endpoints to test with their relative weights
            var endpoints = new List<(string Name, int Weight, Func<CancellationToken, Task<HttpResponseMessage>> Action)>
            {
                ("GetRecentHouses", 30, GetRecentHouses),
                ("GetNearbyHouses", 25, ct => GetNearbyHouses(ct)),
                ("GetHouseById", 20, ct => GetHouseById(ct)),
                ("CreateHouse", 5, ct => CreateHouse(ct)),
                ("DeleteHouse", 1, ct => DeleteHouse(ct))
            };

            // Calculate total weight
            int totalWeight = endpoints.Sum(e => e.Weight);

            // Select random endpoint based on weight
            int randomValue = _random.Next(1, totalWeight + 1);
            int cumulativeWeight = 0;

            foreach (var endpoint in endpoints)
            {
                cumulativeWeight += endpoint.Weight;
                if (randomValue <= cumulativeWeight)
                {
                    return (endpoint.Name, endpoint.Action);
                }
            }

            // Fallback to first endpoint (should never reach here)
            return (endpoints[0].Name, endpoints[0].Action);
        }

        static void UpdateStatistics(string endpoint, bool isSuccess, long responseTimeMs)
        {
            lock (_lockObject)
            {
                _totalRequests++;

                if (isSuccess)
                    _successfulRequests++;
                else
                    _failedRequests++;

                if (!_endpointStats.TryGetValue(endpoint, out var stats))
                {
                    stats = new EndpointStatistics(endpoint);
                    _endpointStats[endpoint] = stats;
                }

                stats.TotalRequests++;

                if (isSuccess)
                    stats.SuccessfulRequests++;
                else
                    stats.FailedRequests++;

                // Update response time statistics
                stats.TotalResponseTime += responseTimeMs;

                if (responseTimeMs < stats.MinResponseTime || stats.MinResponseTime == 0)
                    stats.MinResponseTime = responseTimeMs;

                if (responseTimeMs > stats.MaxResponseTime)
                    stats.MaxResponseTime = responseTimeMs;
            }
        }

        static void DisplayResults()
        {
            Console.WriteLine("\n==================== RESULTS ====================");
            Console.WriteLine($"Total requests: {_totalRequests}");
            Console.WriteLine($"Successful requests: {_successfulRequests} ({(double)_successfulRequests / _totalRequests:P2})");
            Console.WriteLine($"Failed requests: {_failedRequests} ({(double)_failedRequests / _totalRequests:P2})");
            Console.WriteLine($"Requests per second: {_totalRequests / _runTimeSeconds:F2}");

            Console.WriteLine("\nEndpoint statistics:");
            Console.WriteLine("--------------------------------------------");
            Console.WriteLine("Endpoint                Requests  Success   Fail  Avg(ms)  Min(ms)  Max(ms)");
            Console.WriteLine("--------------------------------------------");

            foreach (var stat in _endpointStats.Values.OrderByDescending(s => s.TotalRequests))
            {
                Console.WriteLine($"{stat.Endpoint,-24} {stat.TotalRequests,8} {stat.SuccessPercentage,7:P0} {stat.FailPercentage,6:P0} {stat.AverageResponseTime,8:F1} {stat.MinResponseTime,8} {stat.MaxResponseTime,8}");
            }
        }

        #region API Endpoint Methods

        static async Task<HttpResponseMessage> GetRecentHouses(CancellationToken cancellationToken)
        {
            int count = _random.Next(5, 21); // Random count between 5 and 20
            return await _httpClient.GetAsync($"{_baseUrl}/houses/recent?count={count}", cancellationToken);
        }

        static async Task<HttpResponseMessage> GetNearbyHouses(CancellationToken cancellationToken)
        {
            // Generate random coordinates in a reasonable range
            decimal latitude = (decimal)(_random.NextDouble() * 180 - 90);
            decimal longitude = (decimal)(_random.NextDouble() * 360 - 180);
            decimal radius = (decimal)(_random.Next(1, 51)); // 1-50 km radius

            return await _httpClient.GetAsync(
                $"{_baseUrl}/houses/nearby?latitude={latitude}&longitude={longitude}&radiusInKm={radius}",
                cancellationToken);
        }

        static async Task<HttpResponseMessage> GetHouseById(CancellationToken cancellationToken)
        {
            int id = _random.Next(1, 1001); // Random ID between 1 and 1000
            return await _httpClient.GetAsync($"{_baseUrl}/houses/{id}", cancellationToken);
        }

        static async Task<HttpResponseMessage> CreateHouse(CancellationToken cancellationToken)
        {
            var house = GenerateRandomHouse();
            return await _httpClient.PostAsJsonAsync($"{_baseUrl}/houses", house, _jsonOptions, cancellationToken);
        }

        static async Task<HttpResponseMessage> DeleteHouse(CancellationToken cancellationToken)
        {
            int id = _random.Next(1, 1001); // Random ID between 1 and 1000
            return await _httpClient.DeleteAsync($"{_baseUrl}/houses/{id}", cancellationToken);
        }

        static HouseModel GenerateRandomHouse()
        {
            string[] cities = { "Copenhagen", "Aarhus", "Odense", "Aalborg", "Esbjerg", "Randers", "Kolding", "Horsens", "Vejle", "Roskilde" };
            string[] streets = { "Main St", "Oak Ave", "Pine Rd", "Maple Ln", "Cedar Blvd", "Elm St", "Park Ave", "Lake Rd", "River Dr", "Mountain View" };
            string[] descriptions = {
                "Beautiful modern house with garden",
                "Cozy apartment in the city center",
                "Spacious family home near schools",
                "Luxurious villa with swimming pool",
                "Charming cottage with sea view",
                "Contemporary designer home",
                "Historic house with character",
                "Renovated apartment with balcony",
                "Bright home with open floor plan",
                "Quiet property on a cul-de-sac"
            };

            int cityIndex = _random.Next(cities.Length);
            int streetIndex = _random.Next(streets.Length);
            int descriptionIndex = _random.Next(descriptions.Length);

            // Generate random coordinates based on Denmark's rough geographic bounds
            decimal latitude = (decimal)(55.0 + _random.NextDouble() * 3.0);  // ~55-58 degrees N
            decimal longitude = (decimal)(8.0 + _random.NextDouble() * 5.0);  // ~8-13 degrees E

            return new HouseModel
            {
                Title = $"House in {cities[cityIndex]} on {streets[streetIndex]}",
                Description = descriptions[descriptionIndex],
                Price = (decimal)(_random.Next(100000, 5000001) / 100) * 100, // Random price between 100,000 and 5,000,000 rounded to hundreds
                Latitude = latitude,
                Longitude = longitude,
                ListedDate = DateTime.UtcNow.AddDays(-_random.Next(0, 31)) // Listed 0-30 days ago
            };
        }

        #endregion
    }
}