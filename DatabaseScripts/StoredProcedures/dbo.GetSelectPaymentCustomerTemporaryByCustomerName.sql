CREATE proc [dbo].[GetSelectPaymentCustomerTemporaryByCustomerName]
@CustomerName nvarchar(255)
as
select * from View_SelectPaymentCustomerTemporary
where CustomerName  like N'%'+@CustomerName+N'%'

