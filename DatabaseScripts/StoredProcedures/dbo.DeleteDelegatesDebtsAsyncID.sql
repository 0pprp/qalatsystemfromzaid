
CREATE proc [dbo].[DeleteDelegatesDebtsAsyncID]
@DelegateDebtID int  
as
insert DeleteData (DelegatesDebtsAsyncID) values ((select AsyncID from DelegatesDebts where DelegateDebtID=@DelegateDebtID))

