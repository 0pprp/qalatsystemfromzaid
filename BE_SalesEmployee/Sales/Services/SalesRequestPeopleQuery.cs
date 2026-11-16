namespace BE_SalesEmployee.Sales.Services
{
    /// <summary>
    /// Director source filter travels as <c>people=role|city|id|name~...</c>.
    /// Each branch only receives people whose city matches, as <c>role|id|name</c>.
    /// </summary>
    public static class SalesRequestPeopleQuery
    {
        public static string RewritePath(string companyPath, string cityValue)
        {
            if (string.IsNullOrWhiteSpace(companyPath) || string.IsNullOrWhiteSpace(cityValue))
            {
                return companyPath ?? "";
            }

            var qIndex = companyPath.IndexOf('?', StringComparison.Ordinal);
            if (qIndex < 0)
            {
                return companyPath;
            }

            var basePath = companyPath[..qIndex];
            var query = companyPath[(qIndex + 1)..];
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var order = new List<string>();
            foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = pair.Split('=', 2);
                if (kv.Length != 2)
                {
                    continue;
                }

                var name = Uri.UnescapeDataString(kv[0]);
                var value = Uri.UnescapeDataString(kv[1]);
                if (!map.ContainsKey(name))
                {
                    order.Add(name);
                }

                map[name] = value;
            }

            if (!map.TryGetValue("people", out var people))
            {
                return companyPath;
            }

            var scoped = Scope(people, cityValue);
            if (string.IsNullOrEmpty(scoped))
            {
                map.Remove("people");
                order.RemoveAll(n => string.Equals(n, "people", StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                map["people"] = scoped;
            }

            if (order.Count == 0)
            {
                return basePath;
            }

            var qs = string.Join("&", order.Where(map.ContainsKey).Select(name =>
                $"{Uri.EscapeDataString(name)}={Uri.EscapeDataString(map[name])}"));
            return string.IsNullOrEmpty(qs) ? basePath : $"{basePath}?{qs}";
        }

        public static string Scope(string? people, string cityValue)
        {
            if (string.IsNullOrWhiteSpace(people) || string.IsNullOrWhiteSpace(cityValue))
            {
                return "";
            }

            var kept = new List<string>();
            foreach (var token in people.Split('~', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var parts = token.Split('|');
                if (parts.Length < 3)
                {
                    continue;
                }

                // role|city|id|name. Promoters are not tied to one province: their orders
                // are stored under the customer's branch, matched by name.
                var role = parts[0];
                var promoter = string.Equals(role, "promoter", StringComparison.OrdinalIgnoreCase);
                if (!promoter && !string.Equals(parts[1], cityValue, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var id = parts[2];
                var name = parts.Length >= 4 ? parts[3] : "";
                kept.Add(string.IsNullOrEmpty(name) ? $"{role}|{id}" : $"{role}|{id}|{name}");
            }

            return string.Join("~", kept);
        }
    }
}
