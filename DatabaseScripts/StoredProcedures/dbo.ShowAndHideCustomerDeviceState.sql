CREATE proc [dbo].[ShowAndHideCustomerDeviceState]
as
DECLARE @CustomerDeviceIDHide int = 0
WHILE(1 = 1)
BEGIN
  SELECT @CustomerDeviceIDHide = MIN(CustomerID)
  FROM View_Customers WHERE CustomerID > @CustomerDeviceIDHide and AmountRemaining=0 and CustomerState='true'
  IF @CustomerDeviceIDHide IS NULL BREAK
  update Customers set CustomerState='false' where CustomerID=@CustomerDeviceIDHide 
END
DECLARE @CustomerDeviceIDVisible int = 0
WHILE(1 = 1)
BEGIN
  SELECT @CustomerDeviceIDVisible = MIN(CustomerID)
  FROM View_Customers WHERE CustomerID > @CustomerDeviceIDVisible and AmountRemaining>0 and CustomerState='false'
  IF @CustomerDeviceIDVisible IS NULL BREAK
  update Customers set CustomerState='true' where CustomerID=@CustomerDeviceIDVisible 
END

 

