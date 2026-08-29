CREATE PROCEDURE [dbo].[TrustReceipts_Delete]
    @TrustReceiptID INT,
    @UpdatedByUserID INT = NULL
AS
BEGIN
    UPDATE [dbo].[TrustReceipts]
    SET 
        [IsDelete] = 1,
        [UpdatedByUserID] = @UpdatedByUserID,
        [UpdatedDate] = GETDATE()
    WHERE
        [TrustReceiptID] = @TrustReceiptID;
END
