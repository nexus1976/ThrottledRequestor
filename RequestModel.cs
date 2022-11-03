namespace ThrottledRequestor
{
    internal class RequestModel
    {
        public Guid Id { get; set; }
        public string? Url { get; set; }
        public DateTime RequestDateTime { get; set; }
        public string? ResponseContent { get; set; }

        public static RequestModel Create(Guid id, string url, DateTime requestDateTime)
        {
            var requestModel = new RequestModel()
            {
                Id = id,
                RequestDateTime = requestDateTime,
                Url = url
            };
            return requestModel;
        }
        public static RequestModel Create(string url)
        {
            var requestModel = new RequestModel()
            {
                Id = Guid.NewGuid(),
                RequestDateTime = DateTime.UtcNow,
                Url = url
            };
            return requestModel;
        }
    }
}
