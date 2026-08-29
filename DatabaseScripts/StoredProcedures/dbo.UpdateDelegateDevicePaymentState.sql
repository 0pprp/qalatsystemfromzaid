
CREATE proc [dbo].[UpdateDelegateDevicePaymentState]
@DelegateID int = NULL,
@DevicePaymentState bit
as
update Delegates set DevicePaymentState=DevicePaymentState where DelegateID=@DelegateID

