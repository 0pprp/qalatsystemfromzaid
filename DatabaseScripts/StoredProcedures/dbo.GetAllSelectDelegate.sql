 
CREATE proc [dbo].[GetAllSelectDelegate]
as
select *,
(select AsyncID from Delegates where DelegateID=SelectDelegate.DelegateFatherID)as FatherAsyncID,
(select AsyncID from Delegates where DelegateID=SelectDelegate.DelegateChildID)as ChildAsyncID
from SelectDelegate

