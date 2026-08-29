CREATE proc [dbo].[Stores_Create]
@UserCreateID int,
@StoreName nvarchar(100),
@StorePlace nvarchar(100),
@Notes  nvarchar(max)
as
declare @DBName nvarchar(100) =  DB_NAME()
declare @CityID int = (SELECT dbo.GetCityID(@DBName) AS CityID)
insert into Stores (StoreName,CityID,StorePlace,Notes,State,UserID,AsyncID,AsyncState) values (@StoreName,@CityID,@StorePlace,@Notes,1,@UserCreateID,NEWID(),0)
	INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserCreateID
           ,N'تم اضافة المخزن  '+@StoreName+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
DECLARE @LastId int;
SET @LastId = IDENT_CURRENT('Stores');
select * from View_Stores where StoreID=@LastId 

