using BE_Company.Sales.Authorization;
using BE_Company.Sales.DTO;

namespace BE_Company.Sales.Services
{
    /// <summary>
    /// Same-province Existing/New classification for DM exception review.
    /// Existing = phone OR triple-name only. Kinship never classifies Existing.
    /// </summary>
    public static class ExceptionReviewCustomerMatcher
    {
        public const string ReasonPhone = "تطابق رقم الهاتف";
        public const string ReasonTriple = "تطابق الاسم الثلاثي";
        public const string ReasonBoth = "تطابق الاسم ورقم الهاتف";

        public const string LabelNew = "زبون جديد";
        public const string LabelExisting = "زبون قديم";
        public const string ExplainNew =
            "لم يتم العثور على تطابق في نفس المحافظة برقم الهاتف أو الاسم الثلاثي";

        public static string ExplainExisting(int count) =>
            $"تم العثور على {count} حالة مطابقة في نفس المحافظة";

        /// <summary>
        /// Returns all valid same-province matches. Candidates whose CityValue is missing
        /// are excluded unless <paramref name="branchCityValue"/> stamps them (catalog on branch host).
        /// Cross-province candidates are ignored even when phone/name would match.
        /// </summary>
        public static IReadOnlyList<ExceptionReviewMatchHit> FindMatches(
            string? requestName,
            string? requestPhone,
            string? requestCityValue,
            string? branchCityValue,
            IReadOnlyList<ExceptionReviewCandidate> candidates)
        {
            var requestCity = (requestCityValue ?? branchCityValue ?? "").Trim();
            if (string.IsNullOrWhiteSpace(requestCity))
            {
                return [];
            }

            var hits = new List<ExceptionReviewMatchHit>();
            foreach (var c in candidates ?? [])
            {
                if (c.CustomerId <= 0)
                {
                    continue;
                }

                var candidateCity = ResolveCandidateCity(c.CityValue, branchCityValue);
                if (string.IsNullOrWhiteSpace(candidateCity))
                {
                    // Missing CityValue with no authoritative branch stamp → not a match.
                    continue;
                }

                if (!SameProvince(requestCity, candidateCity))
                {
                    continue;
                }

                var phoneMatch = !string.IsNullOrWhiteSpace(requestPhone)
                                 && SalesPhoneNormalizer.Matches(requestPhone, c.Phone);
                var tripleMatch = SalesRequestNameSimilarity.IsTripleNameMatch(requestName, c.FullName);

                // Kinship alone must NOT create a match.
                if (!phoneMatch && !tripleMatch)
                {
                    continue;
                }

                hits.Add(new ExceptionReviewMatchHit
                {
                    Customer = c,
                    PhoneMatch = phoneMatch,
                    TripleNameMatch = tripleMatch,
                    MatchReasons = BuildReasons(phoneMatch, tripleMatch)
                });
            }

            return hits;
        }

        public static ExceptionReviewClassificationDTO Classify(IReadOnlyList<ExceptionReviewMatchHit> hits)
        {
            var count = hits?.Count ?? 0;
            if (count <= 0)
            {
                return new ExceptionReviewClassificationDTO
                {
                    Type = "New",
                    LabelArabic = LabelNew,
                    MatchCount = 0,
                    ExplanationArabic = ExplainNew
                };
            }

            return new ExceptionReviewClassificationDTO
            {
                Type = "Existing",
                LabelArabic = LabelExisting,
                MatchCount = count,
                ExplanationArabic = ExplainExisting(count)
            };
        }

        public static List<string> BuildReasons(bool phoneMatch, bool tripleMatch)
        {
            if (phoneMatch && tripleMatch)
            {
                return [ReasonBoth];
            }

            if (phoneMatch)
            {
                return [ReasonPhone];
            }

            if (tripleMatch)
            {
                return [ReasonTriple];
            }

            return [];
        }

        /// <summary>
        /// Prefer explicit candidate CityValue; else stamp from branch catalog host.
        /// </summary>
        public static string? ResolveCandidateCity(string? candidateCityValue, string? branchCityValue)
        {
            if (!string.IsNullOrWhiteSpace(candidateCityValue))
            {
                return candidateCityValue.Trim();
            }

            return string.IsNullOrWhiteSpace(branchCityValue) ? null : branchCityValue.Trim();
        }

        public static bool SameProvince(string requestCityValue, string candidateCityValue)
        {
            var a = requestCityValue.Trim();
            var b = candidateCityValue.Trim();
            if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Comparable short keys that differ → different province.
            if (SalesBranchScope.IsComparableBranchKey(a) && SalesBranchScope.IsComparableBranchKey(b))
            {
                return false;
            }

            // Non-comparable labels: require exact (case-insensitive) equality only.
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>Maps SalesRequest source fields to Arabic DM-facing labels.</summary>
    public static class ExceptionReviewSourceMapper
    {
        public const string Unavailable = "غير متوفر";

        public static ExceptionReviewSourceDTO FromRequest(SalesRequestDTO? row, string? listName = null)
        {
            if (row is null)
            {
                return new ExceptionReviewSourceDTO
                {
                    Type = "",
                    DisplayLabel = Unavailable,
                    PersonName = Unavailable,
                    ListName = null,
                    BranchName = Unavailable
                };
            }

            var sourceType = (row.CustomerSourceType ?? "").Trim();
            var person = string.IsNullOrWhiteSpace(row.CreatedByName) ? Unavailable : row.CreatedByName.Trim();
            var branch = string.IsNullOrWhiteSpace(row.CityName)
                ? (string.IsNullOrWhiteSpace(row.CustomerProvince) ? Unavailable : row.CustomerProvince.Trim())
                : row.CityName.Trim();

            string label;
            string? list;
            if (SalesRequestSources.IsDelegate(sourceType))
            {
                label = "مندوب";
                list = ResolveListName(row.SourceListId, listName);
            }
            else if (SalesRequestSources.IsFollower(sourceType))
            {
                label = "متابع";
                list = ResolveListName(row.SourceListId, listName);
            }
            else if (SalesRequestSources.IsEmployeeSubmitted(sourceType))
            {
                label = "موظف مبيعات";
                list = null;
                if (string.IsNullOrWhiteSpace(row.CreatedByName)
                    && !string.IsNullOrWhiteSpace(row.TargetEmployeeName))
                {
                    person = row.TargetEmployeeName.Trim();
                }
            }
            else if (string.Equals(sourceType, SalesRequestSources.NewCustomer, StringComparison.OrdinalIgnoreCase)
                     || string.Equals(sourceType, SalesRequestSources.ExistingCustomer, StringComparison.OrdinalIgnoreCase)
                     || string.IsNullOrWhiteSpace(sourceType))
            {
                // Manager / intake create — use CreatedByUserType when helpful.
                label = MapUserTypeLabel(row.CreatedByUserType) ?? "مدير مبيعات";
                list = null;
                if (person == Unavailable && !string.IsNullOrWhiteSpace(row.CreatedByName))
                {
                    person = row.CreatedByName.Trim();
                }
            }
            else
            {
                label = Unavailable;
                list = ResolveListName(row.SourceListId, listName);
            }

            if (string.IsNullOrWhiteSpace(person))
            {
                person = Unavailable;
            }

            return new ExceptionReviewSourceDTO
            {
                Type = string.IsNullOrWhiteSpace(sourceType) ? "" : sourceType,
                DisplayLabel = label,
                PersonName = person,
                ListName = list,
                BranchName = branch
            };
        }

        private static string? ResolveListName(int? sourceListId, string? listName)
        {
            if (sourceListId is null or <= 0)
            {
                return null;
            }

            return string.IsNullOrWhiteSpace(listName) ? Unavailable : listName.Trim();
        }

        private static string? MapUserTypeLabel(string? userType)
        {
            if (string.IsNullOrWhiteSpace(userType))
            {
                return null;
            }

            var t = userType.Trim();
            if (SalesRoles.IsFollower(t) || string.Equals(t, SalesRequestSources.Follower, StringComparison.OrdinalIgnoreCase))
            {
                return "متابع";
            }

            if (string.Equals(t, SalesRequestSources.Delegate, StringComparison.OrdinalIgnoreCase)
                || t.Contains("مندوب", StringComparison.Ordinal))
            {
                return "مندوب";
            }

            if (SalesRoles.IsSalesEmployee(t))
            {
                return "موظف مبيعات";
            }

            if (SalesRoles.IsSalesManager(t))
            {
                return "مدير مبيعات";
            }

            return null;
        }
    }
}
