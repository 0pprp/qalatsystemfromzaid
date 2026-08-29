CREATE proc [dbo].[GetCustomersSalesCustomerLast]
@DelegateID int
as
select * from View_CustomersSales where DelegateID = @DelegateID and CONVERT(Date,DateCreate)=CONVERT(date,getdate())

