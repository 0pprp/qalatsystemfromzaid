CREATE proc [dbo].[GetDelegateInfo]
@AsyncID nvarchar(255) = NULL
as
select top 1 * from Delegates
where AsyncID=@AsyncID

