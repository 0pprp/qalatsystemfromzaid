using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Data.SqlClient;

namespace BE_Company.Legal;

/// <summary>
/// Drops القانونية customers from every JSON response unless the caller is a legal viewer
/// (see <see cref="LegalVisibility"/>). Works on the response objects, so it covers existing
/// endpoints without touching their stored procedures: rows carrying a CustomerID are checked
/// against the branch database and removed from their list; a single hidden object becomes 404.
/// Not covered: aggregate totals already summed in SQL, and endpoints that return raw JSON text.
/// </summary>
public sealed class LegalCustomerVisibilityFilter : IAsyncResultFilter
{
    private static readonly ConcurrentDictionary<Type, TypePlan> Plans = new();

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        var http = context.HttpContext;
        var configuration = http.RequestServices.GetRequiredService<IConfiguration>();
        var skip = !LegalVisibility.HidingEnabled(configuration)
            || LegalVisibility.IsLegalViewer(http)
            || http.Request.Path.StartsWithSegments("/api/legal-branch");
        if (!skip
            && context.Result is ObjectResult result
            && result.Value != null
            && result.StatusCode is null or >= 200 and < 300)
        {
            try
            {
                await FilterAsync(context, result, configuration);
            }
            catch (Exception ex)
            {
                // Failing closed would break the dashboard; failing open would leak. Report and fail closed
                // only for the response we could not inspect.
                http.RequestServices.GetService<ILogger<LegalCustomerVisibilityFilter>>()
                    ?.LogError(ex, "Legal visibility filter failed for {Path}", http.Request.Path);
                context.Result = new ObjectResult(new { message = "تعذر تجهيز البيانات" }) { StatusCode = 500 };
            }
        }

        await next();
    }

    private static async Task FilterAsync(ResultExecutingContext context, ObjectResult result, IConfiguration configuration)
    {
        var root = result.Value!;
        if (root is not string && root is IEnumerable rootSequence && root is not IDictionary && !IsLeaf(root.GetType()))
        {
            // Materialize once so a lazy sequence is not executed twice.
            var list = new List<object?>();
            foreach (var item in rootSequence)
                list.Add(item);
            root = list;
            result.Value = list;
        }

        var scan = new Scan();
        Walk(root, scan, depth: 0);
        if (scan.CustomerIds.Count == 0 && !scan.SawLegalFlag)
            return;

        var connectionString = configuration.GetConnectionString("DataBaseConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        var hiddenCustomers = new HashSet<int>();
        if (scan.CustomerIds.Count > 0)
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            hiddenCustomers = await LegalVisibility.HiddenCustomerIdsAsync(connection, scan.CustomerIds);
        }

        bool Hidden(object? item)
        {
            if (item is null || !scan.Rows.TryGetValue(item, out var row))
                return false;
            if (row.IsLegal)
                return true;
            return row.CustomerId is int customerId && hiddenCustomers.Contains(customerId);
        }

        if (Hidden(root))
        {
            context.Result = new NotFoundObjectResult(new { message = "غير موجود" });
            return;
        }

        foreach (var list in scan.Lists)
        {
            for (var i = list.Count - 1; i >= 0; i--)
            {
                if (Hidden(list[i]))
                    list.RemoveAt(i);
            }
        }

        foreach (var (owner, property, array) in scan.Arrays)
        {
            if (!array.Cast<object?>().Any(Hidden))
                continue;
            var kept = array.Cast<object?>().Where(item => !Hidden(item)).ToArray();
            var replacement = Array.CreateInstance(property.PropertyType.GetElementType()!, kept.Length);
            Array.Copy(kept, replacement, kept.Length);
            try
            {
                property.SetValue(owner, replacement);
            }
            catch
            {
                // Read-only at runtime: leave it.
            }
        }
    }

    private static void Walk(object node, Scan scan, int depth)
    {
        if (depth > 12 || node is string || !scan.Seen.Add(node))
            return;

        var type = node.GetType();
        if (IsLeaf(type) || node is IDictionary)
            return;

        if (node is IEnumerable sequence)
        {
            if (node is not ICollection)
                return; // nested lazy sequence: do not enumerate twice
            if (node is IList list && !list.IsFixedSize && !list.IsReadOnly)
                scan.Lists.Add(list);
            foreach (var item in sequence)
            {
                if (item != null)
                    Walk(item, scan, depth + 1);
            }

            return;
        }

        if (!IsWalkable(type))
            return;

        var plan = Plans.GetOrAdd(type, BuildPlan);
        var customerId = plan.ReadInt(plan.CustomerId, node);
        var isLegal = plan.ReadBool(plan.IsLegal, node);
        if (customerId is > 0 || isLegal)
        {
            scan.Rows[node] = new Row(customerId is > 0 ? customerId : null, isLegal);
            if (customerId is > 0)
                scan.CustomerIds.Add(customerId.Value);
            scan.SawLegalFlag |= isLegal;
        }

        foreach (var child in plan.Children)
        {
            object? value;
            try
            {
                value = child.GetValue(node);
            }
            catch
            {
                continue;
            }

            if (value is null)
                continue;
            if (value is Array array && array.Rank == 1 && child.CanWrite)
                scan.Arrays.Add((node, child, array));
            Walk(value, scan, depth + 1);
        }
    }

    private static bool IsWalkable(Type type)
    {
        var ns = type.Namespace ?? "";
        return ns.StartsWith("BE_Company", StringComparison.Ordinal)
            || type.Name.Contains("AnonymousType", StringComparison.Ordinal);
    }

    private static bool IsLeaf(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type.IsPrimitive || type.IsEnum)
            return true;
        return type == typeof(decimal) || type == typeof(DateTime) || type == typeof(DateTimeOffset)
            || type == typeof(DateOnly) || type == typeof(TimeOnly) || type == typeof(TimeSpan)
            || type == typeof(Guid) || type == typeof(string) || type == typeof(byte[])
            || (type.Namespace ?? "").StartsWith("System.Text.Json", StringComparison.Ordinal);
    }

    private static TypePlan BuildPlan(Type type)
    {
        var props = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .ToArray();
        PropertyInfo? Find(string name) =>
            props.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        var children = props.Where(p => !IsLeaf(p.PropertyType)).ToArray();
        return new TypePlan(Find("CustomerID"), Find("IsLegal"), children);
    }

    private sealed record Row(int? CustomerId, bool IsLegal);

    private sealed class Scan
    {
        public HashSet<object> Seen { get; } = new(ReferenceEqualityComparer.Instance);
        public Dictionary<object, Row> Rows { get; } = new(ReferenceEqualityComparer.Instance);
        public List<IList> Lists { get; } = new();
        public List<(object Owner, PropertyInfo Property, Array Array)> Arrays { get; } = new();
        public HashSet<int> CustomerIds { get; } = new();
        public bool SawLegalFlag { get; set; }
    }

    private sealed record TypePlan(
        PropertyInfo? CustomerId,
        PropertyInfo? IsLegal,
        PropertyInfo[] Children)
    {
        public int? ReadInt(PropertyInfo? property, object node)
        {
            if (property is null)
                return null;
            try
            {
                return property.GetValue(node) switch
                {
                    int i => i,
                    long l when l is > 0 and <= int.MaxValue => (int)l,
                    _ => null,
                };
            }
            catch
            {
                return null;
            }
        }

        public bool ReadBool(PropertyInfo? property, object node)
        {
            if (property is null)
                return false;
            try
            {
                return property.GetValue(node) is true;
            }
            catch
            {
                return false;
            }
        }
    }
}
