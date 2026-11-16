using System.Text.Json.Nodes;

namespace BE_SalesEmployee.Sales.Services
{
    /// <summary>
    /// Sums per-branch overview payloads. Growth is recomputed from the summed
    /// month and previous month, not averaged across branches.
    /// </summary>
    public static class SalesOverviewBranches
    {
        public static bool IsAqeel(string? name, string? database = null, string? link = null)
        {
            if (!string.IsNullOrEmpty(name) && name.Contains("عقيل", StringComparison.Ordinal))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(database) && database.Contains("Aqeel", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return !string.IsNullOrEmpty(link) && link.Contains("aqeel", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>نظرة عامة only: عقيل، الكوت، and ميسان stay off this page.</summary>
        public static bool IsExcluded(string? name, string? database = null, string? link = null)
        {
            if (IsAqeel(name, database, link))
            {
                return true;
            }

            if (Contains(name, "كوت") || Contains(name, "ميسان"))
            {
                return true;
            }

            if (Contains(database, "Kot") || Contains(database, "Maysan"))
            {
                return true;
            }

            return Contains(link, "kot") || Contains(link, "maysan");
        }

        private static bool Contains(string? value, string part) =>
            !string.IsNullOrEmpty(value) && value.Contains(part, StringComparison.OrdinalIgnoreCase);
    }

    public static class SalesDashboardMerger
    {
        private static readonly string[] IntKeys =
        [
            "employeesOnShift",
            "employeesOffShift",
            "liveLocations",
            "salesToday",
            "pendingSales",
            "newSalesRequests",
            "monthSalesCount",
            "previousMonthSalesCount",
            "monthItemQuantity",
            "ordersCreated",
            "ordersConverted",
            "ordersFinished",
            "ordersRejected",
            "ordersOpen",
            "ordersPending",
            "ordersUnassigned",
            "scoredSalesCount",
            "highScoreSales",
            "lowScoreSales",
            "excellentSales",
            "goodSales",
            "weakSales",
            "laggingSales",
            "badSales"
        ];

        private static readonly string[] DecimalKeys =
        [
            "monthSalePrice",
            "monthCollected",
            "previousMonthSalePrice",
            "monthDownPayment",
            "scoreSum"
        ];

        public static JsonObject Merge(
            IReadOnlyList<(string CityValue, string CityName, JsonObject Body)> branches,
            IReadOnlyList<string> failedBranches)
        {
            var result = new JsonObject();
            var ints = IntKeys.ToDictionary(k => k, _ => 0, StringComparer.OrdinalIgnoreCase);
            var decimals = DecimalKeys.ToDictionary(k => k, _ => 0m, StringComparer.OrdinalIgnoreCase);
            var days = new Dictionary<int, (int Count, decimal Price)>();
            var items = new Dictionary<string, (int Quantity, int Sales, decimal Price)>(StringComparer.OrdinalIgnoreCase);
            var recommenders = new Dictionary<string, (string Name, string Role, int Sales, decimal Price)>(StringComparer.OrdinalIgnoreCase);
            var sellers = new Dictionary<string, SellerSlot>(StringComparer.OrdinalIgnoreCase);
            var scores = new Dictionary<int, (int Count, decimal Price)>();
            var statuses = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var year = 0;
            var month = 0;
            var daysInMonth = 0;

            var branchArray = new JsonArray();
            foreach (var (cityValue, cityName, body) in branches.OrderBy(b => b.CityName, StringComparer.Ordinal))
            {
                body["cityValue"] = cityValue;
                body["cityName"] = cityName;
                body.Remove("branches");
                body.Remove("failedBranches");
                body.Remove("partial");

                foreach (var key in ints.Keys.ToList())
                {
                    ints[key] += ReadInt(body, key);
                }

                foreach (var key in decimals.Keys.ToList())
                {
                    decimals[key] += ReadDecimal(body, key);
                }

                var readYear = ReadInt(body, "year");
                var readMonth = ReadInt(body, "month");
                if (readYear > 0)
                {
                    year = readYear;
                }

                if (readMonth > 0)
                {
                    month = readMonth;
                }
                daysInMonth = Math.Max(daysInMonth, ReadInt(body, "daysInMonth"));
                MergeDays(body, days);
                MergeItems(body, items);
                MergePeople(body, "recommenders", recommenders);
                MergeSellers(body, sellers);
                MergeScores(body, scores);
                MergeStatuses(body, statuses);

                branchArray.Add(JsonNode.Parse(body.ToJsonString()));
            }

            foreach (var pair in ints)
            {
                result[pair.Key] = pair.Value;
            }

            foreach (var pair in decimals)
            {
                result[pair.Key] = pair.Value;
            }

            result["year"] = year;
            result["month"] = month;
            result["daysInMonth"] = daysInMonth;
            result["cityValue"] = "";
            result["cityName"] = "كل الفروع";
            result["salesCountGrowthPercent"] = GrowthNode(ints["monthSalesCount"], ints["previousMonthSalesCount"]);
            result["salePriceGrowthPercent"] = GrowthNode(decimals["monthSalePrice"], decimals["previousMonthSalePrice"]);
            result["collectedPercent"] = CollectedPercent(decimals["monthCollected"], decimals["monthSalePrice"]);
            result["averageSalePrice"] = ints["monthSalesCount"] <= 0
                ? 0
                : Math.Round(decimals["monthSalePrice"] / ints["monthSalesCount"], 0, MidpointRounding.AwayFromZero);
            result["orderConversionPercent"] = ints["ordersCreated"] <= 0
                ? 0
                : Math.Round(ints["ordersConverted"] * 100m / ints["ordersCreated"], 1, MidpointRounding.AwayFromZero);
            result["unscoredSalesCount"] = Math.Max(0, ints["monthSalesCount"] - ints["scoredSalesCount"]);
            var averageScore = ints["scoredSalesCount"] <= 0
                ? (decimal?)null
                : Math.Round(decimals["scoreSum"] / ints["scoredSalesCount"], 1, MidpointRounding.AwayFromZero);
            result["averageSaleScore"] = averageScore == null ? null : JsonValue.Create(averageScore.Value);

            var daily = new JsonArray();
            var lastDay = daysInMonth > 0 ? daysInMonth : (days.Count == 0 ? 0 : days.Keys.Max());
            for (var day = 1; day <= lastDay; day++)
            {
                days.TryGetValue(day, out var slot);
                daily.Add(new JsonObject
                {
                    ["day"] = day,
                    ["salesCount"] = slot.Count,
                    ["salePrice"] = slot.Price
                });
            }

            result["daily"] = daily;
            result["items"] = ToItemArray(items);
            result["categories"] = ToCategoryArray(items);
            result["recommenders"] = ToPeopleArray(recommenders);
            result["sellers"] = ToSellerArray(sellers);
            result["saleScores"] = ToScoreArray(scores);
            result["orderStatuses"] = ToStatusArray(statuses);
            result["branches"] = branchArray;
            result["partial"] = failedBranches.Count > 0;
            var failed = new JsonArray();
            foreach (var name in failedBranches)
            {
                failed.Add(name);
            }

            result["failedBranches"] = failed;
            return result;
        }

        public static decimal CollectedPercent(decimal collected, decimal salePrice)
        {
            if (salePrice <= 0m || collected <= 0m)
            {
                return 0m;
            }

            var capped = collected >= salePrice ? salePrice : collected;
            return Math.Round(capped / salePrice * 100m, 1, MidpointRounding.AwayFromZero);
        }

        public static decimal? GrowthPercent(decimal current, decimal previous)
        {
            if (previous == 0m)
            {
                return current == 0m ? 0m : null;
            }

            return Math.Round((current - previous) / previous * 100m, 1, MidpointRounding.AwayFromZero);
        }

        private static JsonNode? GrowthNode(decimal current, decimal previous)
        {
            var value = GrowthPercent(current, previous);
            return value == null ? null : JsonValue.Create(value.Value);
        }

        private static void MergeDays(JsonObject body, Dictionary<int, (int Count, decimal Price)> days)
        {
            if (body["daily"] is not JsonArray array)
            {
                return;
            }

            foreach (var node in array)
            {
                if (node is not JsonObject row)
                {
                    continue;
                }

                var day = ReadInt(row, "day");
                if (day <= 0)
                {
                    continue;
                }

                days.TryGetValue(day, out var slot);
                days[day] = (slot.Count + ReadInt(row, "salesCount"), slot.Price + ReadDecimal(row, "salePrice"));
            }
        }

        private static void MergeItems(JsonObject body, Dictionary<string, (int Quantity, int Sales, decimal Price)> items)
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

                items.TryGetValue(name, out var slot);
                items[name] = (
                    slot.Quantity + ReadInt(row, "quantity"),
                    slot.Sales + ReadInt(row, "salesCount"),
                    slot.Price + ReadDecimal(row, "salePrice"));
            }
        }

        private static void MergePeople(
            JsonObject body,
            string property,
            Dictionary<string, (string Name, string Role, int Sales, decimal Price)> people)
        {
            if (body[property] is not JsonArray array)
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
                people.TryGetValue(key, out var slot);
                people[key] = (name, role, slot.Sales + ReadInt(row, "salesCount"), slot.Price + ReadDecimal(row, "salePrice"));
            }
        }

        private static void MergeSellers(JsonObject body, Dictionary<string, SellerSlot> sellers)
        {
            if (body["sellers"] is not JsonArray array)
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

                if (!sellers.TryGetValue(name, out var slot))
                {
                    slot = new SellerSlot { Name = name };
                    sellers[name] = slot;
                }

                slot.Sales += ReadInt(row, "salesCount");
                slot.Price += ReadDecimal(row, "salePrice");
                slot.Scored += ReadInt(row, "scoredSales");
                slot.High += ReadInt(row, "highScores");
                slot.Low += ReadInt(row, "lowScores");
                slot.ScoreSum += ReadDecimal(row, "scoreSum");
                slot.Excellent += ReadInt(row, "excellentSales");
                slot.Good += ReadInt(row, "goodSales");
                slot.Weak += ReadInt(row, "weakSales");
                slot.Lagging += ReadInt(row, "laggingSales");
                slot.Bad += ReadInt(row, "badSales");
            }
        }

        private static void MergeScores(JsonObject body, Dictionary<int, (int Count, decimal Price)> scores)
        {
            if (body["saleScores"] is not JsonArray array)
            {
                return;
            }

            foreach (var node in array)
            {
                if (node is not JsonObject row)
                {
                    continue;
                }

                var label = ReadString(row, "label");
                var score = PaymentScore(label, ReadInt(row, "score"));
                if (score is null)
                {
                    continue;
                }

                scores.TryGetValue(score.Value, out var slot);
                scores[score.Value] = (slot.Count + ReadInt(row, "count"), slot.Price + ReadDecimal(row, "salePrice"));
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

        private static JsonArray ToItemArray(Dictionary<string, (int Quantity, int Sales, decimal Price)> items)
        {
            var array = new JsonArray();
            foreach (var pair in items.OrderByDescending(p => p.Value.Price).ThenBy(p => p.Key, StringComparer.Ordinal))
            {
                array.Add(new JsonObject
                {
                    ["itemName"] = pair.Key,
                    ["category"] = CategoryOf(pair.Key),
                    ["quantity"] = pair.Value.Quantity,
                    ["salesCount"] = pair.Value.Sales,
                    ["salePrice"] = pair.Value.Price
                });
            }

            return array;
        }

        private static JsonArray ToCategoryArray(Dictionary<string, (int Quantity, int Sales, decimal Price)> items)
        {
            var grouped = new Dictionary<string, (int Quantity, int Sales, decimal Price)>(StringComparer.Ordinal);
            foreach (var pair in items)
            {
                var category = CategoryOf(pair.Key);
                grouped.TryGetValue(category, out var slot);
                grouped[category] = (
                    slot.Quantity + pair.Value.Quantity,
                    slot.Sales + pair.Value.Sales,
                    slot.Price + pair.Value.Price);
            }

            var array = new JsonArray();
            foreach (var pair in grouped.OrderByDescending(p => p.Value.Price))
            {
                array.Add(new JsonObject
                {
                    ["category"] = pair.Key,
                    ["quantity"] = pair.Value.Quantity,
                    ["salesCount"] = pair.Value.Sales,
                    ["salePrice"] = pair.Value.Price
                });
            }

            return array;
        }

        private static JsonArray ToPeopleArray(Dictionary<string, (string Name, string Role, int Sales, decimal Price)> people)
        {
            var array = new JsonArray();
            foreach (var pair in people.OrderByDescending(p => p.Value.Price).ThenBy(p => p.Value.Name, StringComparer.Ordinal))
            {
                array.Add(new JsonObject
                {
                    ["name"] = pair.Value.Name,
                    ["role"] = pair.Value.Role,
                    ["salesCount"] = pair.Value.Sales,
                    ["salePrice"] = pair.Value.Price
                });
            }

            return array;
        }

        private static JsonArray ToSellerArray(Dictionary<string, SellerSlot> sellers)
        {
            var array = new JsonArray();
            foreach (var pair in sellers.OrderByDescending(p => p.Value.Price).ThenBy(p => p.Value.Name, StringComparer.Ordinal))
            {
                var average = pair.Value.Scored <= 0
                    ? null
                    : (decimal?)Math.Round(pair.Value.ScoreSum / pair.Value.Scored, 1, MidpointRounding.AwayFromZero);
                array.Add(new JsonObject
                {
                    ["name"] = pair.Value.Name,
                    ["role"] = "",
                    ["salesCount"] = pair.Value.Sales,
                    ["salePrice"] = pair.Value.Price,
                    ["scoredSales"] = pair.Value.Scored,
                    ["highScores"] = pair.Value.High,
                    ["lowScores"] = pair.Value.Low,
                    ["excellentSales"] = pair.Value.Excellent,
                    ["goodSales"] = pair.Value.Good,
                    ["weakSales"] = pair.Value.Weak,
                    ["laggingSales"] = pair.Value.Lagging,
                    ["badSales"] = pair.Value.Bad,
                    ["scoreSum"] = pair.Value.ScoreSum,
                    ["averageScore"] = average == null ? null : JsonValue.Create(average.Value)
                });
            }

            return array;
        }

        private static int? PaymentScore(string label, int score)
        {
            if (label == "ممتاز")
            {
                return 10;
            }

            if (label == "جيد")
            {
                return 5;
            }

            if (label == "ضعيف")
            {
                return 0;
            }

            if (label == "متلكئ")
            {
                return -5;
            }

            if (label == "سيء")
            {
                return -10;
            }

            return score is 10 or 5 or 0 or -5 or -10 ? score : null;
        }

        private static string PaymentLabel(int score) => score switch
        {
            10 => "ممتاز",
            5 => "جيد",
            0 => "ضعيف",
            -5 => "متلكئ",
            -10 => "سيء",
            _ => string.Empty
        };

        private static JsonArray ToScoreArray(Dictionary<int, (int Count, decimal Price)> scores)
        {
            var array = new JsonArray();
            foreach (var pair in scores.OrderByDescending(p => p.Key))
            {
                array.Add(new JsonObject
                {
                    ["label"] = PaymentLabel(pair.Key),
                    ["score"] = pair.Key,
                    ["count"] = pair.Value.Count,
                    ["salePrice"] = pair.Value.Price
                });
            }

            return array;
        }

        private sealed class SellerSlot
        {
            public string Name { get; set; } = string.Empty;
            public int Sales { get; set; }
            public decimal Price { get; set; }
            public int Scored { get; set; }
            public int High { get; set; }
            public int Low { get; set; }
            public decimal ScoreSum { get; set; }
            public int Excellent { get; set; }
            public int Good { get; set; }
            public int Weak { get; set; }
            public int Lagging { get; set; }
            public int Bad { get; set; }
        }

        private static JsonArray ToStatusArray(Dictionary<string, int> statuses)
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

        public static string CategoryOf(string? name)
        {
            var text = (name ?? string.Empty).Trim();
            if (text.Length == 0)
            {
                return "أخرى";
            }

            if (Has(text, "دراجة", "بايك", "بايسكل", "سكوتر", "bike", "ebike"))
            {
                return "دراجات";
            }

            if (Has(text, "هاتف", "جوال", "موبايل", "ايفون", "آيفون", "أيفون", "سامسونج", "شاومي", "ريدمي", "phone", "iphone"))
            {
                return "هواتف";
            }

            if (Has(text, "لابتوب", "حاسوب", "كمبيوتر", "laptop"))
            {
                return "حواسيب";
            }

            if (Has(text, "شاشة", "تلفزيون", "تلفاز"))
            {
                return "شاشات";
            }

            return "أخرى";
        }

        private static bool Has(string text, params string[] parts)
        {
            foreach (var part in parts)
            {
                if (text.Contains(part, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static int ReadInt(JsonObject obj, string name)
        {
            if (!TryGet(obj, name, out var node) || node == null)
            {
                return 0;
            }

            if (node is JsonValue value)
            {
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

                if (value.TryGetValue<string>(out var s) && int.TryParse(s, out var parsed))
                {
                    return parsed;
                }
            }

            return 0;
        }

        private static decimal ReadDecimal(JsonObject obj, string name)
        {
            if (!TryGet(obj, name, out var node) || node == null)
            {
                return 0m;
            }

            if (node is JsonValue value)
            {
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

                if (value.TryGetValue<long>(out var l))
                {
                    return l;
                }
            }

            return 0m;
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
            foreach (var pair in obj)
            {
                if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
                {
                    node = pair.Value;
                    return true;
                }
            }

            node = null;
            return false;
        }
    }
}
