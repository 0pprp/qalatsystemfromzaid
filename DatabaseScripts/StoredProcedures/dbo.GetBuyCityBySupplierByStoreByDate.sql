CREATE proc [dbo].[GetBuyCityBySupplierByStoreByDate]
@CityID int = NULL,
@SupplierID int = NULL,
@StoreID  int = NULL,
@FromDate datetime,
@ToDate datetime
as
select * from View_Buys where 
CityID=@CityID and
SupplierID=@SupplierID and
StoreID=@StoreID and
CONVERT(date, DateCreate)>=@FromDate
and 
CONVERT(date, DateCreate)<=@ToDate

