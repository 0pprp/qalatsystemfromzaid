CREATE proc [dbo].[GetBuysBySupplierID]
@SupplierID int = NULL
as
SELECT   * from View_Buys

where SupplierID=@SupplierID

