
CREATE proc [dbo].[UpdateDelegatePhoneNumber]
@DelegateID int = NULL,
@PhoneNumber nvarchar(255)
as
update Delegates set PhoneNumber=@PhoneNumber where DelegateID=@DelegateID

