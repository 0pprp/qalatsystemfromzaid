CREATE proc [dbo].[GetSelectPaymentCustomerTemporaryByDelegate]
@DelegateID int = NULL
as
select * from View_SelectPaymentCustomerTemporary
where DelegateID=@DelegateID

