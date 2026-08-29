 
CREATE proc [dbo].[NumberOfDelegates]
as
select count(*) as NumberOfDelegates from  Delegates where DelegateState='true'


