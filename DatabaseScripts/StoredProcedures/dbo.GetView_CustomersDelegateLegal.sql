CREATE proc [dbo].[GetView_CustomersDelegateLegal]
as
select * from View_CustomersDelegate where IsLegal='true'

