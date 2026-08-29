CREATE proc [dbo].[GetCityInfo]
as
select top 1 *,
(select ISNULL(count(*),0)   from Delegates where DelegateState='true')as NumberOfDelegate,
(select ISNULL(count(*),0)   from Customers where CustomerState='true')as NumberOfCustomer,
(select ISNULL(count(*),0)   from Stores where [State]='true')as NumberOfStore,
(select ISNULL(count(*),0)   from Suppliers where SupplierState='true')as NumberOfSupplier,
(select ISNULL(count(*),0)   from ExchangeItems where ExchangeItemsState='true')As NumberOfExchangeItem,
(select ISNULL(COUNT(*),0)   from Items where Quantity>0)as NumberOfCurrentItem,
(select ISNULL(COUNT(NumberOfItemsBuys) ,0)       from View_Items )as NumberOfItemBuy,
(select ISNULL(COUNT(NumberOfItemsSales),0)       from View_Items )as NumberOfItemSale,
(select ISNULL(sum(CostTotalItem)       ,0)       from View_Items)as AmountCurrentItemBuy,
(select ISNULL(sum(PriceTotalItem)      ,0)       from View_Items)as AmountCuttentItemSale,
(select ISNULL(sum(AmountTotalSales)    ,0)       from View_CustomersDelegate where CustomerState='true')as AmountPrice,
(select ISNULL(sum(CostTotalSales)      ,0)       from View_CustomersDelegate  where CustomerState='true')as AmountCost,
(select ISNULL(sum(AmountDaySales)      ,0)       from View_CustomersDelegate  where CustomerState='true')as AmountDay,
(select ISNULL(sum(ReceiptsTotal)       ,0)       from View_CustomersDelegate  where CustomerState='true')as AmountReceipt,
(select ISNULL(sum(AmountRemaining)     ,0)       from View_CustomersDelegate  where CustomerState='true')as AmountRemaining
from Cities

