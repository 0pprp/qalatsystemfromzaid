from pathlib import Path

# --- Patch WrongSaleExceptionForwarder ---
p = Path(r"C:\GitHub\SalesHaider-main\SalesHaider-main\BE_DelegateWebApplication\BE_DelegateWebApplication\Services\WrongSaleExceptionForwarder.cs")
t = p.read_text(encoding="utf-8")
old = '''        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning(
                "SalesGateway not configured; WrongSale stayed on branch WrongSaleReports only.");
            return WrongSaleExceptionForwardResult.Failure(
                StatusCodes.Status503ServiceUnavailable,
                "بوابة الاستثناء غير مهيأة");
        }

        var payload = new
        {
            request.CityValue,
            request.CityName,
            request.CustomerId,
            request.CustomerName,
            request.CustomerPhone,
            request.SalesRequestId,
            request.Reason,
            TargetApproverType = "DelegatedManager",
            request.RequesterUserId,
            request.RequesterUserName,
            request.RequesterDisplayName,
            Role = "SalesManager"
        };
'''
new = '''        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning(
                "SalesGateway not configured; WrongSale stayed on branch WrongSaleReports only.");
            return WrongSaleExceptionForwardResult.Failure(
                StatusCodes.Status503ServiceUnavailable,
                "بوابة الاستثناء غير مهيأة");
        }

        // Complaints use Branch:CityValue (e.g. DatabaseCompanyNajaf). WrongSale used to send
        // Arabic province/district ("الكوفة") or null → gateway reject «المحافظة مطلوبة».
        var cityValue = ResolveCityValue(request.CityValue);
        var cityName = string.IsNullOrWhiteSpace(request.CityName)
            ? (_configuration["Branch:CityName"] ?? cityValue)
            : request.CityName.Trim();
        if (string.IsNullOrWhiteSpace(cityValue))
        {
            _logger.LogWarning(
                "WrongSale forward aborted: CityValue missing (customer/province empty and Branch:CityValue unset).");
            return WrongSaleExceptionForwardResult.Failure(
                StatusCodes.Status400BadRequest,
                "محافظة الفرع غير مهيأة لإرسال المبيع الخاطئ");
        }

        var payload = new
        {
            CityValue = cityValue,
            CityName = cityName,
            request.CustomerId,
            request.CustomerName,
            request.CustomerPhone,
            request.SalesRequestId,
            request.Reason,
            TargetApproverType = "DelegatedManager",
            request.RequesterUserId,
            request.RequesterUserName,
            request.RequesterDisplayName,
            Role = "SalesManager"
        };
'''
if old not in t:
    raise SystemExit("forwarder payload block not found")
t = t.replace(old, new, 1)

helper = '''
    private string ResolveCityValue(string? raw)
    {
        var configured = (_configuration["Branch:CityValue"]
                          ?? _configuration["SalesGateway:CityValue"]
                          ?? "").Trim();
        var value = (raw ?? "").Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return configured;
        }

        // Arabic district/province labels from customer scope → branch catalog key.
        if (IsNajafFamilyLabel(value))
        {
            return string.IsNullOrWhiteSpace(configured) ? value : configured;
        }

        return value;
    }

    private static bool IsNajafFamilyLabel(string value) =>
        value.Contains("نجف", StringComparison.Ordinal)
        || value.Contains("الكوفة", StringComparison.Ordinal)
        || value.Contains("كوفة", StringComparison.Ordinal)
        || value.Equals("najaf", StringComparison.OrdinalIgnoreCase)
        || value.Equals("najaf-demo", StringComparison.OrdinalIgnoreCase)
        || value.Equals("DatabaseCompanyNajaf", StringComparison.OrdinalIgnoreCase)
        || value.Equals("DatabaseCompanyNajaf_DEMO", StringComparison.OrdinalIgnoreCase);
}
'''
# replace closing brace of class - currently ends with ForwardAsync method then }
if not t.rstrip().endswith("}"):
    raise SystemExit("unexpected file end")
# Insert helper before final class closing brace
idx = t.rfind("}")
t = t[:idx] + helper + "\n"
p.write_text(t, encoding="utf-8")
print("forwarder patched")

# --- FirstCollection: always forward when isWrong (retry if first forward failed) ---
p = Path(r"C:\GitHub\SalesHaider-main\SalesHaider-main\BE_DelegateWebApplication\BE_DelegateWebApplication\Controllers\DelegatesController.cs")
t = p.read_text(encoding="utf-8")
old = '''            var isWrong = FollowerSaleAuthRules.IsWrongSaleRating(row.Rating);
            if (created && isWrong)
            {
                string? province = scope.Value.CityName;
                if (string.IsNullOrWhiteSpace(province) && auth.CityId is > 0)
                {
                    province = await _followerActions.GetCityNameByIdAsync(auth.CityId.Value, ct);
                }

                var reason = FollowerSaleAuthRules.FormatShopEvaluationBlock(
                    row.Rating,
                    row.Note,
                    row.ShopStockEstimatedValue,
                    row.EstimatedDailyRevenue,
                    scope.Value.CustomerName,
                    scope.Value.Phone,
                    row.SaleAmount,
                    evaluatorRole: "مندوب الجباية");

                _ = await _wrongSaleForwarder.ForwardAsync(new WrongSaleExceptionForwardRequest
                {
                    CityValue = province,
                    CityName = province,
                    CustomerId = body.CustomerId,
                    CustomerName = scope.Value.CustomerName,
                    CustomerPhone = scope.Value.Phone,
                    Reason = reason,
                    RequesterUserId = auth.DelegateId.ToString(),
                    RequesterUserName = auth.DelegateName,
                    RequesterDisplayName = string.IsNullOrWhiteSpace(auth.DelegateName)
                        ? "مندوب"
                        : auth.DelegateName.Trim()
                }, ct);
            }
'''
new = '''            var isWrong = FollowerSaleAuthRules.IsWrongSaleRating(row.Rating);
            // Always (re)forward WrongSale — first attempt may have failed silently (city/gateway).
            // Gateway dedupes pending WrongSale for the same customer.
            if (isWrong)
            {
                string? province = scope.Value.CityName;
                if (string.IsNullOrWhiteSpace(province) && auth.CityId is > 0)
                {
                    province = await _followerActions.GetCityNameByIdAsync(auth.CityId.Value, ct);
                }

                var reason = FollowerSaleAuthRules.FormatShopEvaluationBlock(
                    row.Rating,
                    row.Note,
                    row.ShopStockEstimatedValue,
                    row.EstimatedDailyRevenue,
                    scope.Value.CustomerName,
                    scope.Value.Phone,
                    row.SaleAmount,
                    evaluatorRole: "مندوب الجباية");

                var forward = await _wrongSaleForwarder.ForwardAsync(new WrongSaleExceptionForwardRequest
                {
                    CityValue = province,
                    CityName = province,
                    CustomerId = body.CustomerId,
                    CustomerName = scope.Value.CustomerName,
                    CustomerPhone = scope.Value.Phone,
                    Reason = reason,
                    RequesterUserId = auth.DelegateId.ToString(),
                    RequesterUserName = auth.DelegateName,
                    RequesterDisplayName = string.IsNullOrWhiteSpace(auth.DelegateName)
                        ? "مندوب الجباية"
                        : auth.DelegateName.Trim()
                }, ct);
                if (!forward.Ok)
                {
                    // Keep eval saved; surface in logs for ops. Client still gets IsWrongSale.
                }
            }
'''
if old not in t:
    raise SystemExit("FirstCollection forward block not found")
p.write_text(t.replace(old, new, 1), encoding="utf-8")
print("DelegatesController patched")
print("done")
