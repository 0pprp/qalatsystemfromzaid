create proc [dbo].[CustomersPayments_GetByCustomerID]
@CustomerID int
as
select * from View_CustomersPayments where CustomerID=@CustomerID

