create proc [dbo].[SelectDelegate_GetByDelegateID]
@DelegateID int
as
select * from View_SelectDelegate where DelegateFatherID=@DelegateID

