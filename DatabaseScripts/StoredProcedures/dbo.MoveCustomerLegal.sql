CREATE proc [dbo].[MoveCustomerLegal]
@CustomerID int = NULL,
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
           ,N'تم نقل العميل '+(select CustomerName from Customers where CustomerID=@CustomerID)+N' الى القانونية'
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 
update Customers set IsLegal='true' where CustomerID=@CustomerID

