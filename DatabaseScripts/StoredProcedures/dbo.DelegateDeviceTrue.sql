CREATE proc [dbo].[DelegateDeviceTrue]
@DelegateID int = NULL
as
update CustomersSales set AccountZero='true' where DelegateID=@DelegateID  
update CustomersPayments set AccountZero='true' where DelegateID=@DelegateID  

