CREATE proc [dbo].[GetClientDelegateID]
@AsyncID nvarchar(255) = NULL
as
select  top 1 DelegateID from Delegates where AsyncID=@AsyncID

