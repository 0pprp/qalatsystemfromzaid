CREATE proc [dbo].[UpdateNearestFunctionPointCustomer]
@CustomerID int =null ,
@NearestFunctionPoint nvarchar(255) =null
as
update Customers set NearestFunctionPoint=@NearestFunctionPoint where CustomerID=@CustomerID

