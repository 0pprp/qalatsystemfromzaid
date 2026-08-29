CREATE proc [dbo].[InsertDamagedItem]
@UserID int = NULL,
@Reason nvarchar(255) = NULL,
@DamagedItemDate datetime  = NULL
as
 
INSERT INTO [dbo].[DamagedItems]
           ([UserID]
           ,[Reason]
           ,[DamagedItemDate]
           ,[State]
           ,[AsyncState]
		   ,[AsyncID])
     VALUES
           (@UserID 
           ,@Reason 
           ,@DamagedItemDate 
           ,'true'
           ,'false'
		   ,NEWID())
 

