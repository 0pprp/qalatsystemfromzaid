CREATE proc [dbo].[InsertSelectItemsSales]
@UserID int = NULL,
@CustomerSaleID int = NULL,
@ItemID int = NULL,
@Quantity int  = NULL
as
 
INSERT INTO [dbo].[SelectItemsSales]
           ([UserID]
           ,[CustomerSaleID]
           ,[ItemID]
           ,[Quantity]
           ,[AsyncState]
		   ,[AsyncID])
     VALUES
           (@UserID 
           ,@CustomerSaleID 
           ,@ItemID 
           ,@Quantity
		   ,'false'
		   ,NEWID())
 

