 CREATE proc [dbo].[GetCustomersSalesByDate]
 @FromDate datetime,
 @ToDate datetime
 as
 select * from View_CustomersSales
 where CONVERT(date, DateCreate)>=@FromDate
 and CONVERT(date, DateCreate)<=@ToDate

