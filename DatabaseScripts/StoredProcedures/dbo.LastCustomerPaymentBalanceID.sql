CREATE proc [dbo].[LastCustomerPaymentBalanceID]
as
select top 1 CustomerPaymentBalanceID from CustomerPaymentBalance order by CustomerPaymentBalanceID desc

