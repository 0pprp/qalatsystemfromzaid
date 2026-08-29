CREATE proc [dbo].[CheckPasswordValidationInsert]
@AsyncID nvarchar(255) = NULL
as
select * from Delegates where AsyncID=@AsyncID

