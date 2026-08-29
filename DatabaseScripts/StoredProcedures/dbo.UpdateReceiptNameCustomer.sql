CREATE proc [dbo].[UpdateReceiptNameCustomer]
@CustomerID int =null ,
@ReceiptName nvarchar(255) =null
as
update Customers set ReceiptName=@ReceiptName where CustomerID=@CustomerID

