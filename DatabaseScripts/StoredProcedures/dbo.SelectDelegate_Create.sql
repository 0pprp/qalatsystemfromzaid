  
CREATE proc [dbo].[SelectDelegate_Create]
@DelegateFatherID int,
@DelegateChildID int
as
delete from SelectDelegate where DelegateFatherID=@DelegateFatherID and DelegateChildID=@DelegateChildID
insert into SelectDelegate (DelegateFatherID,DelegateChildID) values (@DelegateFatherID,@DelegateChildID)
DECLARE @LastId int;
SET @LastId = IDENT_CURRENT('SelectDelegate');
select * from View_SelectDelegate where SelectDelegateID=@LastId

