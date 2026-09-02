namespace BE_Company.Sales.Services
{
    public sealed class SalesCompleteException : Exception
    {
        public SalesCompleteException(int statusCode, string message) : base(message)
        {
            StatusCode = statusCode;
        }

        public int StatusCode { get; }
    }
}
