CREATE proc [dbo].[MessageDayDeviceCustomer]
@DateCreate datetime,
@DatabaseCity nvarchar(255)
as
select CustomerName, PhoneNumber,(select DelegateName from Delegates where DelegateID=Customers.DelegateID)as DelegateName, (N'اخي الزبون ( '+CustomerName+N' ) هذا كشف حساب بتفاصيل مدفوعاتكم لنا يرجى تدقيق ذلك والاتصال برقم الشكاوى في حال وجود أي اختلاف تم قبض المبلغ '+cast(cast(cast((select round( ISNULL(sum(AmountDenar),0),-3) from View_CustomersPayments where CustomerID=Customers.CustomerID and PaymentDate=@DateCreate) as float) as int) as nvarchar(50))+N' دينار بتاريخ '+(select convert(varchar, convert (datetime,@DateCreate), 101))+N' مجموع الواصل منكم '+cast(cast(cast((select round(ISNULL(sum(ReceiptsTotal),0),-3) from View_Customers where CustomerID=Customers.CustomerID) as float) as int) as nvarchar(50))+N' دينار مجموع الباقي عليكم '+cast(cast(cast((select round(ISNULL(sum(AmountRemaining),0),-3) from View_Customers where CustomerID=Customers.CustomerID) as float) as int) as nvarchar(50))+N' دينار لمعرفة تفاصيل حسابك اضغط على هذا الرابط '+(select Link+(Customers.AsyncID) from  [dbo].[LinkCustomer] where DatabaseLink=@DatabaseCity)+' '+(select top 1 Description from Advertisement)+'')as Message from Customers where (select AmountRemaining from View_Customers where CustomerID=Customers.CustomerID)>0




