 
CREATE proc [dbo].[UpdateCustomer]
@CustomerID int = NULL,
@UserID int = NULL,
@CustomerName nvarchar(255),
@Address nvarchar(255),
@PhoneNumber nvarchar(255),
@ShopName nvarchar(255),
@NearestFunctionPoint nvarchar(255) ,
@ReceiptName nvarchar(255) ,
@SaleName nvarchar(255) =null ,
@Notes nvarchar(max)  =null
as


INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,N'تم تعديل معلومات العميل '+(select CustomerName from Customers where CustomerID=@CustomerID)+N' الى '+@CustomerName+N' و العنوان من '+(select Address from Customers where CustomerID=@CustomerID)+N' الى '+@Address+N' و رقم هاتف من '+(select PhoneNumber from Customers where CustomerID=@CustomerID)+N' الى '+@PhoneNumber+N' واسم المحل من '+(select ShopName from Customers where CustomerID=@CustomerID)+N' الى '+@ShopName+N' و اقرب نقطة دالة من '+(select NearestFunctionPoint from Customers where CustomerID=@CustomerID)+N' الى '+@NearestFunctionPoint+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 
UPDATE [dbo].[Customers]
   SET [CustomerName] = @CustomerName 
      ,[Address] = @Address 
      ,[UserID] = @UserID 
      ,[PhoneNumber] = @PhoneNumber 
      ,[ShopName] = @ShopName 
      ,[NearestFunctionPoint] = @NearestFunctionPoint 
      ,[ReceiptName] = @ReceiptName 
      ,[SaleName] = @SaleName 
      ,[Notes] = @Notes 
 WHERE CustomerID=@CustomerID



