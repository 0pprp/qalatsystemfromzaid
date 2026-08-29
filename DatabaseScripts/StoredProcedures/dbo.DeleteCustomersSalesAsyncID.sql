CREATE proc [dbo].[DeleteCustomersSalesAsyncID]
@CustomerSaleID int  = NULL
as
 Insert into DeleteData (CustomersSalesAsyncID) values ((select AsyncID from CustomersSales where CustomerSaleID=@CustomerSaleID))

