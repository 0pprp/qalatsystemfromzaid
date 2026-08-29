CREATE proc [dbo].[InsertAddToBoxFromBox]
@UserID int = NULL,
@DateCreate datetime = NULL,
@Amount float = NULL,
@BoxID int = NULL,
@Notes nvarchar(max) = NULL 
as
INSERT INTO [dbo].[AddToBox]
([UserID],[DateCreate],[Amount],[BoxID],[Notes],[AsyncState],[AsyncID])
 VALUES (@UserID,@DateCreate,@Amount,@BoxID,@Notes,'false',newID())
       

			
INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,N'تم اضافة المبلغ '+(CONVERT(nvarchar(255),@Amount*1448))+N' الى الخزينة '+(select BoxName from Boxes where BoxID=@BoxID)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 

