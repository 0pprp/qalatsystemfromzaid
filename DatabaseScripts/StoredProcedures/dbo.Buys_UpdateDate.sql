create proc [dbo].[Buys_UpdateDate]
@BuyID int,
@UpdateCreateID int,
@DateCreate datetime 
as
update Buys set DateCreate = @DateCreate where BuyID=@BuyID
INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UpdateCreateID
           ,N'تم تعديل التاريخ شراء العناصر '+(select  top 1 ItemsNames from View_Buys order by BuyID desc)+N' من المورد '+(select  top 1 SupplierName from View_Buys order by BuyID desc)+N' الى المخزن '+(select  top 1 StoreName from View_Buys order by BuyID desc)+' '
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )

