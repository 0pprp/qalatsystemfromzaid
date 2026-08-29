
CREATE proc [dbo].[Suppliers_Create]
@SupplierName nvarchar(100),
@Address nvarchar(100),
@PhoneNumber nvarchar(100),
@Notes nvarchar(100),
@UserCreateID int
as
declare @DBName nvarchar(100) =  DB_NAME()
declare @CityID int = (SELECT dbo.GetCityID(@DBName) AS CityID)
insert into Suppliers (SupplierName,Address,PhoneNumber,UserID,CityID,AsyncID,AsyncState,Notes,SupplierState) values 
(@SupplierName,@Address,@PhoneNumber,@UserCreateID,@CityID,NEWID(),0,@Notes,1)
	INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserCreateID
           ,N'تم اضافة المورد  '+@SupplierName+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
DECLARE @LastId int;
SET @LastId = IDENT_CURRENT('Suppliers');
select * from View_Suppliers where SupplierID=@LastId 


