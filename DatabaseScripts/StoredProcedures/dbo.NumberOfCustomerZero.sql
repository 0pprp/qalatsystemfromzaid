CREATE proc [dbo].[NumberOfCustomerZero]
as
select count(*) as NumberOfCustomerZero from View_CustomersDelegate where AmountRemaining=0

