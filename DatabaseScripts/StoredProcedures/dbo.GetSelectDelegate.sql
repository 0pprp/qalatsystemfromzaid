 
CREATE proc [dbo].[GetSelectDelegate]
@DelegateID int = NULL
as
select *,
(DelegateChildID)As DelegateID,
(select DelegateName from Delegates where DelegateID=SelectDelegate.DelegateChildID)as DelegateName,
(select ReceiptName from Delegates where DelegateID=SelectDelegate.DelegateChildID)as ReceiptName,
(select UpdateReceipt from Delegates where DelegateID=SelectDelegate.DelegateChildID)as UpdateReceipt,
(select DeleteReceipt from Delegates where DelegateID=SelectDelegate.DelegateChildID)as DeleteReceipt,
(select DevicePaymentState from Delegates where DelegateID=SelectDelegate.DelegateChildID)as DevicePaymentState
from  SelectDelegate
where DelegateChildID=@DelegateID

