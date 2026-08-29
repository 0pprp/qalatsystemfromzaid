CREATE proc [dbo].[GetAllDelegateData]
as
select * from Delegates where DelegateState='true'

