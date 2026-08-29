create proc [dbo].[StatisticsApp_GetAll]
as
select 
(select count(*) from Stores where State=1)as NumberOfStores,
(select SUM(Quantity) from Items where ItemState=1)as NumberOfItems,
(select count(*) from Suppliers where SupplierState=1)as NumberOfSuppliers,
(select count(*) from Buys)as NumberOfPurchases,
(select count(*) from Delegates where DelegateState=1)as NumberOfDelegates,
(select count(*) from Customers where CustomerState=1)as NumberOfCustomers,
(select count(*) from CustomersSales)as NumberOfSales,
(select count(*) from CustomersPayments)as NumberOfPayments,
(select count(*) from ExchangeItems where ExchangeItemsState=1)as NumberOfExchangeItems,
(select count(*) from Employees where EmployeeState=1)as NumberOfEmployees,
(select count(*) from Boxes where BoxState=1)as NumberOfCashBoxes,
(select count(*) from AddToBox )as NumberOfAdditionsToBox,
(select count(*) from WithdrawalFromBox )as NumberOfWithdrawalsFromBox,
(select count(*) from TransferBoxs )as NumberOfTransfersBetweenBoxes

