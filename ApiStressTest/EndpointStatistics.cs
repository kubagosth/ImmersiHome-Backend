using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApiStressTest
{
    public class EndpointStatistics
    {
        public string Endpoint { get; }
        public long TotalRequests { get; set; }
        public long SuccessfulRequests { get; set; }
        public long FailedRequests { get; set; }
        public long TotalResponseTime { get; set; }
        public long MinResponseTime { get; set; }
        public long MaxResponseTime { get; set; }

        public double AverageResponseTime => TotalRequests > 0 ? (double)TotalResponseTime / TotalRequests : 0;
        public double SuccessPercentage => TotalRequests > 0 ? (double)SuccessfulRequests / TotalRequests : 0;
        public double FailPercentage => TotalRequests > 0 ? (double)FailedRequests / TotalRequests : 0;

        public EndpointStatistics(string endpoint)
        {
            Endpoint = endpoint;
            MinResponseTime = long.MaxValue;
        }
    }
}
