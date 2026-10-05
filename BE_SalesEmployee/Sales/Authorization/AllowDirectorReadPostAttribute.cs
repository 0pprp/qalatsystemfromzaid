using Microsoft.AspNetCore.Mvc.Filters;

namespace BE_SalesEmployee.Sales.Authorization;

/// <summary>
/// Marks a POST/PUT that only reads data (e.g. evaluate / evaluation-hits).
/// Director may call these despite <see cref="DenyDirectorWritesAttribute"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class AllowDirectorReadPostAttribute : Attribute, IFilterMetadata
{
}
