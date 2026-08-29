

CREATE proc [dbo].[GetDelegateSelect]
@DelegateID int
as
select * from View_SelectDelegate where DelegateFatherID=@DelegateID

