CREATE PROCEDURE [dbo].[TrustReceipts_GetByID]
    @TrustReceiptID INT
AS
BEGIN
    SELECT *
    FROM [dbo].[TrustReceipts]
    WHERE [TrustReceiptID] = @TrustReceiptID AND [IsDelete] = 0;
END
