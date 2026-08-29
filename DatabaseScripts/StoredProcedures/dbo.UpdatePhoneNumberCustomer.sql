CREATE proc [dbo].[UpdatePhoneNumberCustomer]
@CustomerID int = NULL,
@PhoneNumber nvarchar(255)
as
update Customers set PhoneNumber=@PhoneNumber where CustomerID=@CustomerID

