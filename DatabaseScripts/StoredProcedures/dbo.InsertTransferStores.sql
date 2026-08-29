CREATE proc [dbo].[InsertTransferStores]
@FromStoreID int = NULL,
@ToStoreID int = NULL,
@UserID int = NULL,
@TransferStoreDate datetime= NULL
as
 

INSERT INTO [dbo].[TransferStores]
           ([FromStoreID]
           ,[ToStoreID]
           ,[UserID]
           ,[TransferStoreDate]
           ,[State]
           ,[AsyncState] 
		   ,[AsyncID])
     VALUES
           (@FromStoreID 
           ,@ToStoreID 
           ,@UserID 
           ,@TransferStoreDate 
           ,'true'
           ,'false'
		   ,NEWID())
 


