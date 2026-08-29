CREATE proc [dbo].[GetDelegateNames]
as
select DelegateID,DelegateName from Delegates where DelegateState='true'

