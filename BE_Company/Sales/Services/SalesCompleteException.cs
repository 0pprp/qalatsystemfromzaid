namespace BE_Company.Sales.Services
{
    public sealed class SalesCompleteException : Exception
    {
        public SalesCompleteException(int statusCode, string message, string? code = null) : base(message)
        {
            StatusCode = statusCode;
            Code = code;
        }

        public int StatusCode { get; }
        public string? Code { get; }
    }
}
