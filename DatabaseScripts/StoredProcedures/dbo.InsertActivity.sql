CREATE proc [dbo].[InsertActivity]
@UserID  int = NULL,
@ActivityDescription nvarchar(max) = NULL,
@ActivityDate datetime = NULL
as
INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState])
     VALUES
           (@UserID ,
		   @ActivityDescription,
		   @ActivityDate,
		   'false')

