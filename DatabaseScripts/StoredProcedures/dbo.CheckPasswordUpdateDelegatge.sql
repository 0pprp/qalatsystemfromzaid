CREATE proc [dbo].[CheckPasswordUpdateDelegatge]
@AsyncID nvarchar(255) = NULL
as
select  AsyncID from Delegates where   AsyncID=@AsyncID

