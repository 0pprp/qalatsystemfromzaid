CREATE proc [dbo].[InsertSelectDamagedItem]
@DamagedItemID int = NULL,
@UserID int = NULL,
@ItemID int = NULL,
@DamagedItemQuantity int = NULL
as
INSERT INTO [dbo].[SelectDamagedItems]
           ([DamagedItemID]
           ,[UserID]
           ,[ItemID]
           ,[DamagedItemQuantity]
           ,[AsyncState] 
		   ,[AsyncID])
     VALUES
           (@DamagedItemID 
           ,@UserID 
           ,@ItemID 
           ,@DamagedItemQuantity 
           ,'false'
		   ,NEWID())
 


