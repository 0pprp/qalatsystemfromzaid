-- Sale requests the company marked "not sold" showed agents a generic text; the company's own reason (written by the
-- sales employee) was kept only as an internal note. Show the stored reason instead, and make the timeline note public.
UPDATE agents.Orders
SET PublicReason = LEFT(LTRIM(RTRIM(BranchRejectReason)), 400), UpdatedAt = SYSUTCDATETIME()
WHERE Status = 'NotSold'
  AND PublicReason = N'لم يتم إتمام البيع بعد الكشف الميداني. يمكنك التواصل مع الشركة لمزيد من التفاصيل.'
  AND LEN(LTRIM(RTRIM(ISNULL(BranchRejectReason, N'')))) > 0;
GO
-- The agent's timeline: put the reason on the public "not sold" step (the internal copy stays for the dashboard).
UPDATE pub
SET Note = LEFT(LTRIM(SUBSTRING(internal.Note, LEN(N'سبب المنظومة:') + 1, 4000)), 400)
FROM agents.OrderEvents pub
JOIN agents.OrderEvents internal
  ON internal.OrderId = pub.OrderId AND internal.Status = N'NotSold' AND internal.IsInternal = 1 AND internal.Note LIKE N'سبب المنظومة:%'
WHERE pub.Status = N'NotSold' AND pub.IsInternal = 0 AND pub.Note IS NULL;
