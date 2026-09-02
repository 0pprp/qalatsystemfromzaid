
using BE_Company.IRepository;
using BE_Company.Repository;

public static class ServicesConfiguration
{
    public static void RegisterRepositories(this IServiceCollection services)
    {
        services.AddScoped<IErrorsRepository, ErrorsRepository>();
        services.AddScoped<IAccountsRepository, AccountsRepository>();
        services.AddScoped<IBuysRepository, BuysRepository>();
        services.AddScoped<ICustomersPaymentsRepository, CustomersPaymentsRepository>();
        services.AddScoped<ICustomersRepository, CustomersRepository>();
        services.AddScoped<ICustomersSalesRepository, CustomersSalesRepository>();
        services.AddScoped<IDelegatesRepository, DelegatesRepository>();
        services.AddScoped<IEmployeesRepository, EmployeesRepository>();
        services.AddScoped<IExchangesItemsRepository, ExchangesItemsRepository>();
        services.AddScoped<IStoresRepository, StoresRepository>();
        services.AddScoped<IUsersRepository, UsersRepository>();
        services.AddScoped<IItemsRepository, ItemsRepository>();
        services.AddScoped<ISuppliersRepository, SuppliersRepository>();
        services.AddScoped<IStatisticsAppRepository, StatisticsAppRepository>();
        services.AddScoped<IErrorsRepository, ErrorsRepository>();
        services.AddScoped<ICustomersPaymentsRequestRepository, CustomersPaymentsRequestRepository>();
        services.AddScoped<IBackupDatabaseRepository, BackupDatabaseRepository>();
        services.AddScoped<ICustomerDecisionsRepository, CustomerDecisionsRepository>();
        services.AddScoped<ISalesEmployeeRepository, SalesEmployeeRepository>();
    }
}
