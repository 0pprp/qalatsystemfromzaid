CREATE proc [dbo].[CheckPasswordValidationUpdate]
@DelegateID int = NULL,
@AsyncID nvarchar(255) = NULL
as
select * from Delegates where DelegateID!=@DelegateID and AsyncID=@AsyncID

