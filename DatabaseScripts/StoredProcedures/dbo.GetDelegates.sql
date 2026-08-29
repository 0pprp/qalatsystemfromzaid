CREATE proc [dbo].[GetDelegates]
as
select * from View_Delegates where DelegateState='true'

