using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;

namespace BE_DelegateWebApplication.Services;

/// <summary>
/// Strips الواصل / الباقي (and the figures that reconstruct them) from القانونية
/// customers before the response is serialized. Sale-order prices are left intact.
/// </summary>
public static class LegalCustomerAmountRedactor
{
    public const string MainAccountantUserType = "محاسب رئيسي";

    private static readonly HashSet<string> BalanceFields = new(StringComparer.Ordinal)
    {
        "ReceiptsTotal",
        "ReceiptsTotalBalance",
        "AmountRemaining",
        "AmountRemainingBalance",
        "AmountPaid",
        "PaidAmount",
        "RemainingAmount",
        "Remaining",
        "TotalPaid",
        "CurrentDebt",
        "LastPaymentAmount",
        "ReceiptRate",
        "ReceiptRateDevice",
        "ReceiptRateBalance",
        "WeekPaid",
        "PaidPercent",
        "PaidTodayAmount",
        "AmountDenar",
        "Amount1",
        "Amount2",
        "Amount3",
        "Amount4",
        "Amount5",
        "Amount6",
        "Amount7",
    };

    private static readonly ConcurrentDictionary<Type, TypePlan> Plans = new();

    public static bool IsMainAccountant(string? userType) =>
        string.Equals(userType?.Trim(), MainAccountantUserType, StringComparison.Ordinal);

    public static void Redact(object? graph)
    {
        if (graph is null)
            return;

        Walk(graph, new HashSet<object>(ReferenceEqualityComparer.Instance), ancestorLegal: false);
    }

    private static void Walk(object node, HashSet<object> seen, bool ancestorLegal)
    {
        if (!seen.Add(node))
            return;

        if (node is string)
            return;

        if (node is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                if (item != null)
                    Walk(item, seen, ancestorLegal);
            }

            return;
        }

        var type = node.GetType();
        if (IsLeaf(type))
            return;

        var plan = Plans.GetOrAdd(type, BuildPlan);
        var legal = ancestorLegal || plan.ReadLegal(node);
        if (legal)
            plan.Clear(node);

        foreach (var childProp in plan.Children)
        {
            object? child;
            try
            {
                child = childProp.GetValue(node);
            }
            catch
            {
                continue;
            }

            if (child != null)
                Walk(child, seen, legal);
        }
    }

    private static bool IsLeaf(Type type)
    {
        if (type.IsPrimitive || type.IsEnum)
            return true;

        return type == typeof(decimal)
            || type == typeof(DateTime)
            || type == typeof(DateTimeOffset)
            || type == typeof(TimeSpan)
            || type == typeof(Guid)
            || type == typeof(string);
    }

    private static TypePlan BuildPlan(Type type)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;
        var props = type.GetProperties(flags).Where(p => p.GetIndexParameters().Length == 0).ToArray();
        var legal = props.FirstOrDefault(p => p.Name == "IsLegal" && p.CanRead);
        var hide = props.FirstOrDefault(p => p.Name == "HidePaidRemaining" && p.CanWrite && p.PropertyType == typeof(bool));
        var clear = new List<PropertyInfo>();
        var children = new List<PropertyInfo>();
        var paymentHistory = type.Name.Contains("Payment", StringComparison.Ordinal)
            && !type.Name.Contains("Request", StringComparison.Ordinal);

        foreach (var prop in props)
        {
            if (!prop.CanRead)
                continue;

            if (prop.CanWrite && (BalanceFields.Contains(prop.Name) || (paymentHistory && prop.Name == "Amount")))
            {
                clear.Add(prop);
                continue;
            }

            if (prop.CanWrite && prop.Name == "HidePaidRemaining")
                continue;

            var pt = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
            if (!IsLeaf(pt))
                children.Add(prop);
        }

        return new TypePlan(legal, hide, clear, children);
    }

    private sealed class TypePlan
    {
        private readonly PropertyInfo? _isLegal;
        private readonly PropertyInfo? _hide;
        private readonly PropertyInfo[] _clear;
        private readonly PropertyInfo[] _children;

        public TypePlan(PropertyInfo? isLegal, PropertyInfo? hide, List<PropertyInfo> clear, List<PropertyInfo> children)
        {
            _isLegal = isLegal;
            _hide = hide;
            _clear = clear.ToArray();
            _children = children.ToArray();
        }

        public IReadOnlyList<PropertyInfo> Children => _children;

        public bool ReadLegal(object node)
        {
            if (_isLegal == null)
                return false;

            var value = _isLegal.GetValue(node);
            return value is bool flag && flag;
        }

        public void Clear(object node)
        {
            foreach (var prop in _clear)
            {
                try
                {
                    prop.SetValue(node, Zero(prop.PropertyType));
                }
                catch
                {
                    // Leave unwritable runtime values alone.
                }
            }

            if (_hide != null)
                _hide.SetValue(node, true);
        }

        private static object? Zero(Type type)
        {
            var underlying = Nullable.GetUnderlyingType(type);
            if (underlying != null)
                return null;

            if (type == typeof(string))
                return null;

            if (type.IsValueType)
                return Activator.CreateInstance(type);

            return null;
        }
    }
}
