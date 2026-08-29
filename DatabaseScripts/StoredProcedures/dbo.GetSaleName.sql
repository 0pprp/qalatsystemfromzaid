CREATE proc [dbo].[GetSaleName]
as
select SaleName from Customers where SaleName is not null and SaleName!= '' group by SaleName 

