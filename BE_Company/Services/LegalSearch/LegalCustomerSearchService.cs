using BE_Company.DTO;
using BE_Company.IRepository;

namespace BE_Company.Services.LegalSearch;

public interface ILegalCustomerSearchService
{
    Task<LegalSearchEngineResult> SearchAsync(IReadOnlyList<LegalSearchQueryInput> queries, CancellationToken ct);
}

public sealed class LegalCustomerSearchService : ILegalCustomerSearchService
{
    private readonly ICustomersRepository _customers;

    public LegalCustomerSearchService(ICustomersRepository customers)
    {
        _customers = customers;
    }

    public async Task<LegalSearchEngineResult> SearchAsync(
        IReadOnlyList<LegalSearchQueryInput> queries,
        CancellationToken ct)
    {
        if (queries.Count == 0)
        {
            return new LegalSearchEngineResult();
        }

        if (queries.Count > LegalCustomerNameMatch.MaxQueries)
        {
            throw new InvalidOperationException($"الحد الأقصى {LegalCustomerNameMatch.MaxQueries} صف في الاستيراد");
        }

        // Same branch catalog accountant already sees (LinkCity BE_Company instance).
        var all = await _customers.Customers_GetAll(null, null, "الجميع");
        var catalog = all?
            .Where(c => c.CustomerID is > 0 && (c.CustomerState is null or true))
            .ToList()
            ?? new List<CustomersGetDTO>();

        ct.ThrowIfCancellationRequested();
        return LegalCustomerSearchEngine.Run(catalog, queries);
    }
}
