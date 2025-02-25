using ApiStressTest;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ImmersiHome.StressTest
{
    class Program
    {
        private static readonly Random _random = new Random();
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        // API configuration
        private static string _baseUrl = "https://milkdrift.com/api"; // Default URL to your hosted API
        private static int _concurrentUsers = 10000;
        private static int _runTimeSeconds = 60;
        private static int _rampUpTimeSeconds = 5;
        private static bool _verbose = false;

        // Statistics
        private static long _totalRequests = 0;
        private static long _successfulRequests = 0;
        private static long _failedRequests = 0;
        private static readonly ConcurrentDictionary<string, EndpointStatistics> _endpointStats = new();
        private static readonly object _statsLock = new object();

        // Cache of known house IDs to reduce 404 errors
        private static readonly ConcurrentBag<int> _knownHouseIds = new();
        private static bool _isIdCacheInitialized = false;
        private static readonly SemaphoreSlim _idCacheSemaphore = new SemaphoreSlim(1, 1);

        static async Task Main(string[] args)
        {
            Console.WriteLine("ImmersiHome API Stress Test Tool");
            Console.WriteLine("===============================");

            ParseArguments(args);

            // Configure optimized HttpClient
            HttpClient = CreateOptimizedHttpClient();

            // Initialize house ID cache to reduce 404 errors
            await InitializeHouseIdCacheAsync();

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
            Console.WriteLine("  -url, --url <url>       API base URL (default: https://milkdrift.com/api)");
            Console.WriteLine("  -u, --users <number>    Number of concurrent users (default: 100)");
            Console.WriteLine("  -t, --time <seconds>    Test duration in seconds (default: 60)");
            Console.WriteLine("  -r, --ramp <seconds>    Ramp-up time in seconds (default: 5)");
            Console.WriteLine("  -v, --verbose           Enable verbose output");
            Console.WriteLine("  -h, --help              Show help information");
        }

        // Singleton HttpClient for better performance
        private static HttpClient? _httpClient;
        private static HttpClient HttpClient
        {
            get => _httpClient ??= CreateOptimizedHttpClient();
            set => _httpClient = value;
        }

        private static HttpClient CreateOptimizedHttpClient()
        {
            var handler = new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(15),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
                MaxConnectionsPerServer = 10000,
                EnableMultipleHttp2Connections = true,
                KeepAlivePingPolicy = HttpKeepAlivePingPolicy.WithActiveRequests,
                KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
                KeepAlivePingDelay = TimeSpan.FromSeconds(60)
            };

            var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

            client.DefaultRequestHeaders.Add("User-Agent", "ImmersiHome-StressTest/1.0");
            return client;
        }

        static async Task InitializeHouseIdCacheAsync()
        {
            try
            {
                Console.WriteLine("Initializing house ID cache to reduce 404 errors...");

                // Pre-fetch some house IDs to reduce 404 errors during testing
                var response = await HttpClient.GetAsync($"{_baseUrl}/houses/recent?count=100");
                if (response.IsSuccessStatusCode)
                {
                    var houses = await response.Content.ReadFromJsonAsync<List<HouseModel>>(_jsonOptions);
                    if (houses != null)
                    {
                        foreach (var house in houses)
                        {
                            _knownHouseIds.Add(house.Id);
                        }
                        Console.WriteLine($"Cached {_knownHouseIds.Count} house IDs");
                    }
                }
                else
                {
                    Console.WriteLine($"Warning: Could not prefetch house IDs. Status: {response.StatusCode}");
                    // Fallback: Add some likely IDs
                    for (int i = 1; i <= 100; i++)
                    {
                        _knownHouseIds.Add(i);
                    }
                }

                _isIdCacheInitialized = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing house ID cache: {ex.Message}");
                // Continue anyway with empty cache
                _isIdCacheInitialized = true;
            }
        }

        static async Task RunStressTest()
        {
            Console.WriteLine("\nStarting stress test...");

            var cancellationTokenSource = new CancellationTokenSource();
            var token = cancellationTokenSource.Token;

            // Wait for ID cache to be ready
            while (!_isIdCacheInitialized)
            {
                await Task.Delay(100, token);
            }

            var tasks = new List<Task>();
            var startTime = DateTime.UtcNow;

            // User partitioning for better resource management
            const int usersPerBatch = 500;
            var batchCount = (_concurrentUsers + usersPerBatch - 1) / usersPerBatch;

            // Start user tasks with batched ramp-up
            Console.WriteLine($"Ramping up {_concurrentUsers} users in {batchCount} batches...");

            for (int batchIndex = 0; batchIndex < batchCount; batchIndex++)
            {
                int startUserId = batchIndex * usersPerBatch;
                int endUserId = Math.Min((batchIndex + 1) * usersPerBatch, _concurrentUsers);
                int usersInBatch = endUserId - startUserId;

                Console.WriteLine($"Starting batch {batchIndex + 1}: Adding {usersInBatch} users");

                for (int userId = startUserId; userId < endUserId; userId++)
                {
                    var userTask = SimulateUserActivity(userId, token);
                    tasks.Add(userTask);
                }

                // Delay between batches to prevent thundering herd
                if (batchIndex < batchCount - 1 && _rampUpTimeSeconds > 0)
                {
                    int batchDelay = _rampUpTimeSeconds * 1000 / batchCount;
                    await Task.Delay(batchDelay, token);
                }
            }

            Console.WriteLine($"All {_concurrentUsers} users are now active");
            Console.WriteLine($"Test running for {_runTimeSeconds} seconds...");

            // Wait for the specified duration
            await Task.Delay(TimeSpan.FromSeconds(_runTimeSeconds), token);

            // Stop the test
            Console.WriteLine("\nStopping test...");
            cancellationTokenSource.Cancel();

            try
            {
                // Wait for tasks to complete with a timeout
                await Task.WhenAny(
                    Task.WhenAll(tasks),
                    Task.Delay(TimeSpan.FromSeconds(10))
                );
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation token is used
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during test shutdown: {ex.Message}");
            }

            var endTime = DateTime.UtcNow;
            var elapsedSeconds = (endTime - startTime).TotalSeconds;

            Console.WriteLine("\nTest completed!");
            Console.WriteLine($"Actual test duration: {elapsedSeconds:F2} seconds");
        }

        static async Task SimulateUserActivity(int userId, CancellationToken cancellationToken)
        {
            // Vary think time by user type
            int thinkTimeBaseMs = userId % 3 switch
            {
                0 => 20,   // Power users (very short delays)
                1 => 100,  // Average users
                _ => 200   // Casual users (longer delays)
            };

            // Track created houses by this user to delete them later
            var createdHouseIds = new List<int>();

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Randomly select an API endpoint to call with weighting
                    var endpoint = GetRandomEndpoint(userId, createdHouseIds);

                    if (_verbose && userId % 100 == 0)
                        Console.WriteLine($"User {userId} calling {endpoint.Name}");

                    var stopwatch = Stopwatch.StartNew();
                    var result = await endpoint.Action(cancellationToken);
                    stopwatch.Stop();

                    // Track created house IDs
                    if (endpoint.Name == "CreateHouse" && result.Response.IsSuccessStatusCode && result.CreatedId > 0)
                    {
                        createdHouseIds.Add(result.CreatedId);

                        // Also add to global cache of known IDs
                        _knownHouseIds.Add(result.CreatedId);
                    }

                    // Update statistics
                    UpdateStatistics(endpoint.Name, result.Response.IsSuccessStatusCode, stopwatch.ElapsedMilliseconds);

                    if (_verbose && userId % 100 == 0)
                    {
                        string statusInfo = result.Response.IsSuccessStatusCode
                            ? "success"
                            : $"failed ({result.Response.StatusCode})";
                        Console.WriteLine($"User {userId} completed {endpoint.Name}: {statusInfo} in {stopwatch.ElapsedMilliseconds}ms");
                    }

                    // Random delay between requests to simulate user think time
                    int thinkTime = thinkTimeBaseMs + _random.Next(thinkTimeBaseMs / 2);
                    await Task.Delay(thinkTime, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Test was canceled, exit gracefully
                    break;
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref _totalRequests);
                    Interlocked.Increment(ref _failedRequests);

                    if (_verbose && userId % 100 == 0)
                        Console.WriteLine($"User {userId} error: {ex.Message}");

                    // Add delay before retry on error
                    await Task.Delay(100, cancellationToken);
                }
            }
        }

        static (string Name, Func<CancellationToken, Task<ApiResult>> Action) GetRandomEndpoint(int userId, List<int> userCreatedIds)
        {
            // Adjust weights based on available house IDs
            int deleteWeight = 1;
            int getByIdWeight = 20;

            // Increase delete weight if this user has created houses
            if (userCreatedIds.Count > 0)
            {
                deleteWeight = 10;
            }

            // Define the API endpoints to test with their relative weights
            var endpoints = new List<(string Name, int Weight, Func<CancellationToken, Task<ApiResult>> Action)>
            {
                ("GetRecentHouses", 30, GetRecentHouses),
                ("GetNearbyHouses", 25, ct => GetNearbyHouses(ct)),
                ("GetHouseById", getByIdWeight, ct => GetHouseById(ct, userCreatedIds)),
                ("CreateHouse", 15, ct => CreateHouse(ct, userId)),
                ("DeleteHouse", deleteWeight, ct => DeleteHouse(ct, userCreatedIds))
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
            Interlocked.Increment(ref _totalRequests);

            if (isSuccess)
                Interlocked.Increment(ref _successfulRequests);
            else
                Interlocked.Increment(ref _failedRequests);

            var stats = _endpointStats.GetOrAdd(endpoint, new EndpointStatistics(endpoint));

            Interlocked.Increment(ref stats.TotalRequests);

            if (isSuccess)
                Interlocked.Increment(ref stats.SuccessfulRequests);
            else
                Interlocked.Increment(ref stats.FailedRequests);

            Interlocked.Add(ref stats.TotalResponseTime, responseTimeMs);

            // Update min/max response times with interlocked operations
            long currentMin;
            do
            {
                currentMin = stats.MinResponseTime;
                if (responseTimeMs >= currentMin && currentMin != 0)
                    break;
            } while (Interlocked.CompareExchange(ref stats.MinResponseTime, responseTimeMs, currentMin) != currentMin);

            long currentMax;
            do
            {
                currentMax = stats.MaxResponseTime;
                if (responseTimeMs <= currentMax)
                    break;
            } while (Interlocked.CompareExchange(ref stats.MaxResponseTime, responseTimeMs, currentMax) != currentMax);
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

        static async Task<ApiResult> GetRecentHouses(CancellationToken cancellationToken)
        {
            int count = _random.Next(5, 21); // Random count between 5 and 20
            var response = await HttpClient.GetAsync($"{_baseUrl}/houses/recent?count={count}", cancellationToken);

            // If successful, capture house IDs for future use
            if (response.IsSuccessStatusCode)
            {
                try
                {
                    var houses = await response.Content.ReadFromJsonAsync<List<HouseModel>>(_jsonOptions, cancellationToken);
                    if (houses != null)
                    {
                        foreach (var house in houses)
                        {
                            if (!_knownHouseIds.Contains(house.Id))
                            {
                                _knownHouseIds.Add(house.Id);
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore parsing errors
                }
            }

            return new ApiResult { Response = response };
        }

        static async Task<ApiResult> GetNearbyHouses(CancellationToken cancellationToken)
        {
            // Generate random coordinates in a reasonable range (approximately Denmark)
            decimal latitude = (decimal)(55.0 + _random.NextDouble() * 3.0);  // ~55-58 degrees N
            decimal longitude = (decimal)(8.0 + _random.NextDouble() * 5.0);  // ~8-13 degrees E
            decimal radius = (decimal)(_random.Next(1, 51)); // 1-50 km radius

            var response = await HttpClient.GetAsync(
                $"{_baseUrl}/houses/nearby?latitude={latitude}&longitude={longitude}&radiusInKm={radius}",
                cancellationToken);

            // If successful, capture house IDs for future use
            if (response.IsSuccessStatusCode)
            {
                try
                {
                    var houses = await response.Content.ReadFromJsonAsync<List<HouseModel>>(_jsonOptions, cancellationToken);
                    if (houses != null)
                    {
                        foreach (var house in houses)
                        {
                            if (!_knownHouseIds.Contains(house.Id))
                            {
                                _knownHouseIds.Add(house.Id);
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore parsing errors
                }
            }

            return new ApiResult { Response = response };
        }

        static async Task<ApiResult> GetHouseById(CancellationToken cancellationToken, List<int> userCreatedIds)
        {
            int id;

            // If we have known IDs, use one of them to reduce 404 errors
            if (_knownHouseIds.Count > 0)
            {
                // Prefer IDs created by this user if available
                if (userCreatedIds.Count > 0 && _random.Next(100) < 70)
                {
                    id = userCreatedIds[_random.Next(userCreatedIds.Count)];
                }
                else
                {
                    // Use a known ID from the global cache
                    var knownIds = _knownHouseIds.ToArray();
                    id = knownIds[_random.Next(knownIds.Length)];
                }
            }
            else
            {
                // Fallback to random ID if we have no known IDs yet
                id = _random.Next(1, 1001);
            }

            var response = await HttpClient.GetAsync($"{_baseUrl}/houses/{id}", cancellationToken);
            return new ApiResult { Response = response };
        }

        static async Task<ApiResult> CreateHouse(CancellationToken cancellationToken, int userId)
        {
            var house = GenerateRandomHouse(userId);
            var response = await HttpClient.PostAsJsonAsync($"{_baseUrl}/houses", house, _jsonOptions, cancellationToken);

            int createdId = 0;

            // Extract the created ID if possible
            if (response.IsSuccessStatusCode)
            {
                try
                {
                    var responseHouse = await response.Content.ReadFromJsonAsync<HouseModel>(_jsonOptions, cancellationToken);
                    if (responseHouse != null)
                    {
                        createdId = responseHouse.Id;
                    }
                }
                catch
                {
                    // Ignore parsing errors
                }
            }

            return new ApiResult { Response = response, CreatedId = createdId };
        }

        static async Task<ApiResult> DeleteHouse(CancellationToken cancellationToken, List<int> userCreatedIds)
        {
            int id;

            // Prefer to delete houses created by this user if available
            if (userCreatedIds.Count > 0)
            {
                int index = _random.Next(userCreatedIds.Count);
                id = userCreatedIds[index];
                userCreatedIds.RemoveAt(index); // Remove so we don't try to delete it again
            }
            else if (_knownHouseIds.Count > 0)
            {
                // Otherwise use a known ID to reduce 404 errors
                var knownIds = _knownHouseIds.ToArray();
                id = knownIds[_random.Next(knownIds.Length)];

                // Try to remove this ID from known IDs (best effort)
                _knownHouseIds.TryTake(out _);
            }
            else
            {
                // Last resort - use a random ID
                id = _random.Next(1, 1001);
            }

            var response = await HttpClient.DeleteAsync($"{_baseUrl}/houses/{id}", cancellationToken);
            return new ApiResult { Response = response };
        }

        static HouseModel GenerateRandomHouse(int userId)
        {
            string[] cities = { "Copenhagen", "Aarhus", "Odense", "Aalborg", "Esbjerg", "Randers", "Kolding", "Horsens", "Vejle", "Roskilde" };
            string[] streets = { "Main St", "Oak Ave", "Pine Rd", "Maple Ln", "Cedar Blvd", "Elm St", "Park Ave", "Lake Rd", "River Dr", "Mountain View" };
            string[] adjectives = { "Beautiful", "Spacious", "Cozy", "Luxurious", "Modern", "Charming", "Elegant", "Stunning", "Bright", "Peaceful" };
            string[] features = { "garden", "balcony", "terrace", "fireplace", "pool", "view", "garage", "basement", "attic", "open floor plan" };

            var city = cities[_random.Next(cities.Length)];
            var street = streets[_random.Next(streets.Length)];
            var adjective = adjectives[_random.Next(adjectives.Length)];
            var feature = features[_random.Next(features.Length)];

            // Add some variation based on userId to ensure diverse data
            string userSalt = (userId % 1000).ToString("D3");

            // Generate random coordinates (approximately Denmark)
            decimal latitude = (decimal)(55.0 + _random.NextDouble() * 3.0);  // ~55-58 degrees N
            decimal longitude = (decimal)(8.0 + _random.NextDouble() * 5.0);  // ~8-13 degrees E

            var description = $"{adjective} home in {city} with {feature}. This property offers great value and comfort. Stress test property #{userSalt}.";

            return new HouseModel
            {
                Title = $"StressTest-{userSalt}: {adjective} House on {street}, {city}",
                Description = description,
                Price = (decimal)(_random.Next(100000, 5000001) / 100) * 100, // Random price between 100,000 and 5,000,000 rounded to hundreds
                Latitude = latitude,
                Longitude = longitude,
                ListedDate = DateTime.UtcNow.AddDays(-_random.Next(0, 31)) // Listed 0-30 days ago
            };
        }

        #endregion
    }
}