CREATE proc [dbo].[GetCustomersSalesBySaleName]
@SaleName nvarchar(100)
as
select * from View_CustomersSales where SaleName=@SaleName

