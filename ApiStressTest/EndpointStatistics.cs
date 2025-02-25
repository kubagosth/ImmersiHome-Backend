namespace ApiStressTest
{
    public class EndpointStatistics
    {
        public string Endpoint { get; }
        public long TotalRequests;
        public long SuccessfulRequests;
        public long FailedRequests;
        public long TotalResponseTime;
        public long MinResponseTime;
        public long MaxResponseTime;

        public double AverageResponseTime => TotalRequests > 0 ? (double)TotalResponseTime / TotalRequests : 0;
        public double SuccessPercentage => TotalRequests > 0 ? (double)SuccessfulRequests / TotalRequests : 0;
        public double FailPercentage => TotalRequests > 0 ? (double)FailedRequests / TotalRequests : 0;

        public EndpointStatistics(string endpoint)
        {
            Endpoint = endpoint;
            TotalRequests = 0;
            SuccessfulRequests = 0;
            FailedRequests = 0;
            TotalResponseTime = 0;
            MinResponseTime = long.MaxValue;
            MaxResponseTime = 0;
        }
    }
}
