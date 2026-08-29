CREATE proc [dbo].[CustomerBalanceTrue]
@CustomerID int = NULL
as
update CustomerSaleBalance set AccountZero='true' where CustomerID=@CustomerID  
update CustomerPaymentBalance set AccountZero='true' where CustomerID=@CustomerID  

