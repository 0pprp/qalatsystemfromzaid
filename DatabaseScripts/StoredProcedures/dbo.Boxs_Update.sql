CREATE proc [dbo].[Boxs_Update]
@BoxID int,
@BoxName nvarchar(100),
@UpdateUserID int
as
update Boxes set BoxName = @BoxName where BoxID=@BoxID
INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UpdateUserID
           ,N'تم تعديل الخزينة '+@BoxName+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )

