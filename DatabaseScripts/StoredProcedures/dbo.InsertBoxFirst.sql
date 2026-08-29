CREATE proc [dbo].[InsertBoxFirst]
@BoxName nvarchar(255)= NULL 
as
 
INSERT INTO [dbo].[Boxes]
           ([BoxName]
           ,[AsyncState]
           ,[BoxState]
		   ,[AsyncID])
     VALUES
           (@BoxName,'false','true', NEWID())
 
  

