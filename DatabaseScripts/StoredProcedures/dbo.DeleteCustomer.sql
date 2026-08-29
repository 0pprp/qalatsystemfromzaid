CREATE proc [dbo].[DeleteCustomer]
@CustomerID int = NULL,
@UserID int = NULL
as 
update Customers set CustomerState='false'
where CustomerID=@CustomerID


INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,N'تم حذف العميل '+(select CustomerName from Customers where CustomerID=@CustomerID)+N' معرفة '+CONVERT(nvarchar(255),@CustomerID)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 

