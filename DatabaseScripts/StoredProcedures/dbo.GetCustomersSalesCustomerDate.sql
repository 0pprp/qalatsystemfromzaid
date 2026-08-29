CREATE proc [dbo].[GetCustomersSalesCustomerDate]
@CustomerID int,
@DateCreate datetime
as
select * from View_CustomersSales where CustomerID = @CustomerID and CONVERT(date,DateCreate)=CONVERT(date,@DateCreate)

