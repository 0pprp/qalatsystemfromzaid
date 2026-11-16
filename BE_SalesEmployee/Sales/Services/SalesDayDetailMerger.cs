using System.Text.Json.Nodes;

namespace BE_SalesEmployee.Sales.Services
{
    public static class SalesDayDetailMerger
    {
        public static JsonObject Merge(
            IReadOnlyList<(string CityValue, string CityName, JsonObject Body)> branches,
            IReadOnlyList<string> failedBranches)
        {
            var salesCount = 0;
            var ordersAdded = 0;
            var ordersCompleted = 0;
            var ordersRejected = 0;
            var itemQuantity = 0;
            decimal salePrice = 0;
            decimal downPayment = 0;
            var year = 0;
            var month = 0;
            var day = 0;
            var sellers = new Dictionary<string, (string Name, int Count, decimal Price)>(StringComparer.Ordinal);
            var creators = new Dictionary<string, (string Name, string Role, int Count)>(StringComparer.Ordinal);
            var agents = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
            var statuses = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var items = new Dictionary<string, (string Category, int Quantity, int Sales, decimal Price)>(StringComparer.OrdinalIgnoreCase);
            var branchArray = new JsonArray();

            foreach (var (cityValue, cityName, body) in branches.OrderBy(b => b.CityName, StringComparer.Ordinal))
            {
                salesCount += ReadInt(body, "salesCount");
                ordersAdded += ReadInt(body, "ordersAdded");
                ordersCompleted += ReadInt(body, "ordersCompleted");
                ordersRejected += ReadInt(body, "ordersRejected");
                itemQuantity += ReadInt(body, "itemQuantity");
                salePrice += ReadDecimal(body, "salePrice");
                downPayment += ReadDecimal(body, "downPayment");
                var readYear = ReadInt(body, "year");
                var readMonth = ReadInt(body, "month");
                var readDay = ReadInt(body, "day");
                if (readYear > 0)
                {
                    year = readYear;
                }

                if (readMonth > 0)
                {
                    month = readMonth;
                }

                if (readDay > 0)
                {
                    day = readDay;
                }
                MergePeople(body, "sellers", sellers, withPrice: true);
                MergeCreators(body, creators);
                MergeAgents(body, agents);
                MergeStatuses(body, statuses);
                MergeItems(body, items);
                branchArray.Add(new JsonObject
                {
                    ["cityValue"] = cityValue,
                    ["cityName"] = cityName,
                    ["salesCount"] = ReadInt(body, "salesCount"),
                    ["salePrice"] = ReadDecimal(body, "salePrice"),
                    ["ordersAdded"] = ReadInt(body, "ordersAdded"),
                    ["downPayment"] = ReadDecimal(body, "downPayment"),
                    ["itemQuantity"] = ReadInt(body, "itemQuantity")
                });
            }

            var result = new JsonObject
            {
                ["year"] = year,
                ["month"] = month,
                ["day"] = day,
                ["cityValue"] = "",
                ["cityName"] = branches.Count == 1 ? branches[0].CityName : "كل الفروع",
                ["salesCount"] = salesCount,
                ["salePrice"] = salePrice,
                ["averageSalePrice"] = salesCount <= 0
                    ? 0
                    : Math.Round(salePrice / salesCount, 0, MidpointRounding.AwayFromZero),
                ["downPayment"] = downPayment,
                ["itemQuantity"] = itemQuantity,
                ["ordersAdded"] = ordersAdded,
                ["ordersCompleted"] = ordersCompleted,
                ["ordersRejected"] = ordersRejected,
                ["sellers"] = ToPeople(sellers),
                ["creators"] = ToCreators(creators),
                ["agents"] = ToAgents(agents),
                ["orderStatuses"] = ToStatuses(statuses),
                ["items"] = ToItems(items),
                ["branches"] = branchArray,
                ["partial"] = failedBranches.Count > 0
            };
            var failed = new JsonArray();
            foreach (var name in failedBranches)
            {
                failed.Add(name);
            }

            result["failedBranches"] = failed;
            return result;
        }

        private static void MergePeople(
            JsonObject body,
            string key,
            Dictionary<string, (string Name, int Count, decimal Price)> people,
            bool withPrice)
        {
            if (body[key] is not JsonArray array)
            {
                return;
            }

            foreach (var node in array)
            {
                if (node is not JsonObject row)
                {
                    continue;
                }

                var name = ReadString(row, "name");
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = "بدون اسم";
                }

                people.TryGetValue(name, out var slot);
                people[name] = (
                    name,
                    slot.Count + ReadInt(row, "count"),
                    slot.Price + (withPrice ? ReadDecimal(row, "salePrice") : 0));
            }
        }

        private static void MergeCreators(
            JsonObject body,
            Dictionary<string, (string Name, string Role, int Count)> creators)
        {
            if (body["creators"] is not JsonArray array)
            {
                return;
            }

            foreach (var node in array)
            {
                if (node is not JsonObject row)
                {
                    continue;
                }

                var name = ReadString(row, "name");
                var role = ReadString(row, "role");
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = "بدون اسم";
                }

                var key = name + "|" + role;
                creators.TryGetValue(key, out var slot);
                creators[key] = (name, role, slot.Count + ReadInt(row, "count"));
            }
        }

        private static void MergeAgents(JsonObject body, Dictionary<string, Dictionary<string, int>> agents)
        {
            if (body["agents"] is not JsonArray array)
            {
                return;
            }

            foreach (var node in array)
            {
                if (node is not JsonObject row)
                {
                    continue;
                }

                var name = ReadString(row, "name");
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = "غير مسند";
                }

                if (!agents.TryGetValue(name, out var statuses))
                {
                    statuses = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    agents[name] = statuses;
                }

                if (row["statuses"] is not JsonArray statusArray)
                {
                    continue;
                }

                foreach (var statusNode in statusArray)
                {
                    if (statusNode is not JsonObject statusRow)
                    {
                        continue;
                    }

                    var status = ReadString(statusRow, "status");
                    if (string.IsNullOrWhiteSpace(status))
                    {
                        continue;
                    }

                    statuses.TryGetValue(status, out var count);
                    statuses[status] = count + ReadInt(statusRow, "count");
                }
            }
        }

        private static void MergeStatuses(JsonObject body, Dictionary<string, int> statuses)
        {
            if (body["orderStatuses"] is not JsonArray array)
            {
                return;
            }

            foreach (var node in array)
            {
                if (node is not JsonObject row)
                {
                    continue;
                }

                var status = ReadString(row, "status");
                if (string.IsNullOrWhiteSpace(status))
                {
                    continue;
                }

                statuses.TryGetValue(status, out var count);
                statuses[status] = count + ReadInt(row, "count");
            }
        }

        private static void MergeItems(
            JsonObject body,
            Dictionary<string, (string Category, int Quantity, int Sales, decimal Price)> items)
        {
            if (body["items"] is not JsonArray array)
            {
                return;
            }

            foreach (var node in array)
            {
                if (node is not JsonObject row)
                {
                    continue;
                }

                var name = ReadString(row, "itemName");
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = "بدون اسم";
                }

                var category = ReadString(row, "category");
                items.TryGetValue(name, out var slot);
                items[name] = (
                    string.IsNullOrWhiteSpace(slot.Category) ? category : slot.Category,
                    slot.Quantity + ReadInt(row, "quantity"),
                    slot.Sales + ReadInt(row, "salesCount"),
                    slot.Price + ReadDecimal(row, "salePrice"));
            }
        }

        private static JsonArray ToPeople(Dictionary<string, (string Name, int Count, decimal Price)> people)
        {
            var array = new JsonArray();
            foreach (var pair in people.OrderByDescending(p => p.Value.Price).ThenBy(p => p.Value.Name, StringComparer.Ordinal))
            {
                array.Add(new JsonObject
                {
                    ["name"] = pair.Value.Name,
                    ["role"] = "",
                    ["count"] = pair.Value.Count,
                    ["salePrice"] = pair.Value.Price
                });
            }

            return array;
        }

        private static JsonArray ToCreators(Dictionary<string, (string Name, string Role, int Count)> creators)
        {
            var array = new JsonArray();
            foreach (var pair in creators.OrderByDescending(p => p.Value.Count).ThenBy(p => p.Value.Name, StringComparer.Ordinal))
            {
                array.Add(new JsonObject
                {
                    ["name"] = pair.Value.Name,
                    ["role"] = pair.Value.Role,
                    ["count"] = pair.Value.Count,
                    ["salePrice"] = 0
                });
            }

            return array;
        }

        private static JsonArray ToAgents(Dictionary<string, Dictionary<string, int>> agents)
        {
            var array = new JsonArray();
            foreach (var pair in agents.OrderByDescending(p => p.Value.Values.Sum()).ThenBy(p => p.Key, StringComparer.Ordinal))
            {
                var statuses = new JsonArray();
                foreach (var status in pair.Value.OrderByDescending(s => s.Value))
                {
                    statuses.Add(new JsonObject
                    {
                        ["status"] = status.Key,
                        ["count"] = status.Value
                    });
                }

                array.Add(new JsonObject
                {
                    ["name"] = pair.Key,
                    ["orders"] = pair.Value.Values.Sum(),
                    ["statuses"] = statuses
                });
            }

            return array;
        }

        private static JsonArray ToStatuses(Dictionary<string, int> statuses)
        {
            var array = new JsonArray();
            foreach (var pair in statuses.OrderByDescending(p => p.Value))
            {
                array.Add(new JsonObject
                {
                    ["status"] = pair.Key,
                    ["count"] = pair.Value
                });
            }

            return array;
        }

        private static JsonArray ToItems(Dictionary<string, (string Category, int Quantity, int Sales, decimal Price)> items)
        {
            var array = new JsonArray();
            foreach (var pair in items.OrderByDescending(p => p.Value.Price).ThenBy(p => p.Key, StringComparer.Ordinal))
            {
                array.Add(new JsonObject
                {
                    ["itemName"] = pair.Key,
                    ["category"] = pair.Value.Category,
                    ["quantity"] = pair.Value.Quantity,
                    ["salesCount"] = pair.Value.Sales,
                    ["salePrice"] = pair.Value.Price
                });
            }

            return array;
        }

        private static int ReadInt(JsonObject obj, string name)
        {
            if (!TryGet(obj, name, out var node) || node is not JsonValue value)
            {
                return 0;
            }

            if (value.TryGetValue<int>(out var n))
            {
                return n;
            }

            if (value.TryGetValue<long>(out var l))
            {
                return (int)l;
            }

            if (value.TryGetValue<decimal>(out var d))
            {
                return (int)d;
            }

            return 0;
        }

        private static decimal ReadDecimal(JsonObject obj, string name)
        {
            if (!TryGet(obj, name, out var node) || node is not JsonValue value)
            {
                return 0;
            }

            if (value.TryGetValue<decimal>(out var d))
            {
                return d;
            }

            if (value.TryGetValue<double>(out var dbl))
            {
                return (decimal)dbl;
            }

            if (value.TryGetValue<int>(out var n))
            {
                return n;
            }

            return 0;
        }

        private static string ReadString(JsonObject obj, string name)
        {
            if (!TryGet(obj, name, out var node) || node is not JsonValue value)
            {
                return string.Empty;
            }

            return value.TryGetValue<string>(out var text) ? text ?? string.Empty : string.Empty;
        }

        private static bool TryGet(JsonObject obj, string name, out JsonNode? node)
        {
            if (obj.TryGetPropertyValue(name, out node))
            {
                return true;
            }

            var match = obj.FirstOrDefault(p => string.Equals(p.Key, name, StringComparison.OrdinalIgnoreCase));
            node = match.Value;
            return match.Key != null;
        }
    }
}
