CREATE proc [dbo].[GetDelegateByAsyncID]
@AsyncID nvarchar(255) = NULL
as
select  top 1 DelegateID from Delegates where AsyncID=@AsyncID

