namespace BE_DelegateWebApplication.DTO
{
    public class PagedResultDTO<T>
    {
        public IEnumerable<T> Data { get; set; } = new List<T>();
        public int TotalCount { get; set; }
    }
}
