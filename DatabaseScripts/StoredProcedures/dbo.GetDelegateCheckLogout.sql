CREATE proc [dbo].[GetDelegateCheckLogout]
@AsyncID nvarchar(100)
as
select * from View_Delegates where AsyncID=@AsyncID

