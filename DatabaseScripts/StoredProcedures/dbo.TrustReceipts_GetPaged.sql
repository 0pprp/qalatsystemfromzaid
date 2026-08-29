ALTER PROCEDURE [dbo].[TrustReceipts_GetPaged]
    @SearchTerm NVARCHAR(255) = NULL,
    @PageNumber INT = 1,
    @PageSize INT = 10,
    @DelegateID INT = NULL,
    @TotalCount INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    -- Calculate total count before pagination
    SELECT @TotalCount = COUNT(*)
    FROM [dbo].[TrustReceipts]
    WHERE [IsDelete] = 0
      AND (@DelegateID IS NULL OR [DelegateID] = @DelegateID OR [CreatedByUserID] = @DelegateID)
      AND (@SearchTerm IS NULL OR @SearchTerm = '' OR [BuyerName] LIKE '%' + @SearchTerm + '%');

    -- Fetch paginated results
    SELECT *
    FROM [dbo].[TrustReceipts]
    WHERE [IsDelete] = 0
      AND (@DelegateID IS NULL OR [DelegateID] = @DelegateID OR [CreatedByUserID] = @DelegateID)
      AND (@SearchTerm IS NULL OR @SearchTerm = '' OR [BuyerName] LIKE '%' + @SearchTerm + '%')
    ORDER BY [TrustReceiptID] DESC
    OFFSET (@PageNumber - 1) * @PageSize ROWS
    FETCH NEXT @PageSize ROWS ONLY;
END
