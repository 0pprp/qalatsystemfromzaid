create proc [dbo].[SelectDelegate_Delete]
@SelectDelegateID int
as
delete from SelectDelegate where SelectDelegateID = @SelectDelegateID

