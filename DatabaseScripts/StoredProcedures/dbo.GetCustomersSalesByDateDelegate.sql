 CREATE proc [dbo].[GetCustomersSalesByDateDelegate]
 @DelegateID int = NULL,
 @FromDate datetime,
 @ToDate datetime
 as
 select * from View_CustomersSales
 where DelegateID=@DelegateID and CONVERT(date, DateCreate)>=@FromDate
 and CONVERT(date, DateCreate)<=@ToDate

