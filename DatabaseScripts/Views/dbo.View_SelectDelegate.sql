 

create   view [dbo].[View_SelectDelegate]
AS
SELECT        dbo.SelectDelegate.SelectDelegateID, dbo.SelectDelegate.DelegateFatherID, dbo.SelectDelegate.DelegateChildID AS DelegateID, dbo.SelectDelegate.UserID, dbo.SelectDelegate.AsyncState, 
                         dbo.Delegates.DelegateName AS DelegateFatherName, Delegates_1.DelegateName, Delegates_1.UserID AS Expr1, Delegates_1.CityID, Delegates_1.Address, Delegates_1.PhoneNumber, Delegates_1.Notes, 
                         Delegates_1.DeviceSaleState, Delegates_1.DevicePaymentState, Delegates_1.ReceiptName, Delegates_1.UpdateReceipt, Delegates_1.DeleteReceipt, Delegates_1.AsyncID
FROM            dbo.SelectDelegate LEFT OUTER JOIN
                         dbo.Delegates ON dbo.SelectDelegate.DelegateFatherID = dbo.Delegates.DelegateID LEFT OUTER JOIN
                         dbo.Delegates AS Delegates_1 ON dbo.SelectDelegate.DelegateChildID = Delegates_1.DelegateID 
 
 



