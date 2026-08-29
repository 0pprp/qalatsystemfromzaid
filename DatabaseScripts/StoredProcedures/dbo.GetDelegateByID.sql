CREATE proc [dbo].[GetDelegateByID]
@DelegateID int = NULL
as
select * from View_Delegates where DelegateState='true' and DelegateID=@DelegateID

