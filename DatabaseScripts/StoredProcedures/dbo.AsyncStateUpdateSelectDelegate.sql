
CREATE proc [dbo].[AsyncStateUpdateSelectDelegate]
@SelectDelegateID int = NULL
as
update SelectDelegate set AsyncState='true' where SelectDelegateID=@SelectDelegateID

