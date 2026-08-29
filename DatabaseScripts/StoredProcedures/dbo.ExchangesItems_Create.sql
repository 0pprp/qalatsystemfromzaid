CREATE proc [dbo].[ExchangesItems_Create]
@ExchangeItemName nvarchar(100),
@LimitAmount float,
@UserCreateID int
as
declare @DBName nvarchar(100) =  DB_NAME()
declare @CityID int = (SELECT dbo.GetCityID(@DBName) AS CityID)
insert into ExchangeItems (UserID, ExchangeItemName,LimitAmount,ExchangeItemsState,CityID,AsyncID,AsyncState)values
(@UserCreateID,@ExchangeItemName,@LimitAmount,1,@CityID,NEWID(),0)
	INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserCreateID
           ,N'تم اضافة بند الصرف '+@ExchangeItemName+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
DECLARE @LastId int;
SET @LastId = IDENT_CURRENT('ExchangeItems');
SELECT * FROM View_ExchangeItems WHERE ExchangeItemID =  @LastId


