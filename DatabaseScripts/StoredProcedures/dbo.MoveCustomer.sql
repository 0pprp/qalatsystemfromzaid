CREATE proc [dbo].[MoveCustomer]
@CustomerID int = NULL,
@DelegateID int = NULL,
@UserID int = NULL
as

INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,N'تم نقل العميل '+(select CustomerName from Customers where CustomerID=@CustomerID)+N' من القائمة '+(select DelegateName from View_Customers where CustomerID=@CustomerID)+N' الى القائمة '+(select DelegateName from Delegates where DelegateID=@DelegateID)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
update Customers set DelegateID=@DelegateID where CustomerID=@CustomerID
update CustomersSales set DelegateID=@DelegateID where CustomerID=@CustomerID
update CustomersPayments set DelegateID=@DelegateID where CustomerID=@CustomerID

