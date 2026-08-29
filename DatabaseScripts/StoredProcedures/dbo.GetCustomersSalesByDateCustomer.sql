CREATE proc [dbo].[GetCustomersSalesByDateCustomer]
@FromDate datetime ,
@ToDate datetime, 
@CustomerID nvarchar(255)
as
select * from View_CustomersSales
where 
CustomerID=@CustomerID and
CONVERT(date, DateCreate)>=@FromDate and
CONVERT(date, DateCreate)<=@ToDate  

