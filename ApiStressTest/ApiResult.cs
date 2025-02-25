namespace ApiStressTest
{
    public class ApiResult
    {
        public HttpResponseMessage Response { get; set; } = new HttpResponseMessage();
        public int CreatedId { get; set; } = 0;
    }
}
