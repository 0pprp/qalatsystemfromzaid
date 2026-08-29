CREATE proc [dbo].[DelegateDeviceFalse]
@DelegateID int = NULL
as
update CustomersSales set AccountZero='false' where DelegateID=@DelegateID  
update CustomersPayments set AccountZero='false' where DelegateID=@DelegateID  

