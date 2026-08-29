CREATE proc [dbo].[InsertBuysItems]
@UserID int = NULL,
@BuyID int = NULL,
@ItemID int = NULL,
@Quantity int = NULL
as
 
INSERT INTO [dbo].[BuysItems]
           ([UserID]
           ,[BuyID]
           ,[ItemID]
           ,[Quantity]
		   ,[AsyncID]
		   ,[AsyncState])
     VALUES
(@UserID ,
@BuyID ,
@ItemID ,
@Quantity,
NEWID(),
'false')
 


