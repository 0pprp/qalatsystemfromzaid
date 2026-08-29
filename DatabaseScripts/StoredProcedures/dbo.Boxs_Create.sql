 
CREATE proc [dbo].[Boxs_Create]
@BoxName nvarchar(100),
@CreateUserID int
as
insert into Boxes (BoxName,AsyncID,AsyncState,BoxState) values (@BoxName,NEWID(),0,1)
INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@CreateUserID
           ,N'تم اضافة الخزينة '+@BoxName+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )

