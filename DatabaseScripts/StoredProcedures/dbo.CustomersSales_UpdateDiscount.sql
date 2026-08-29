 
CREATE proc [dbo].[CustomersSales_UpdateDiscount]
@CustomerSaleID int,
@DiscountAmountTotal float,
@DiscountAmountTotalDay float,
@UserUpdateID int,
@DateCreate datetime
as
update CustomersSales set DiscountAmountTotal=@DiscountAmountTotal , DiscountAmountTotalDay = @DiscountAmountTotalDay,DateCreate = @DateCreate where CustomerSaleID=@CustomerSaleID
INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserUpdateID
           ,N'تم  تعديل تاريخ البيع '+(select  top 1 ItemsNames from View_CustomersSales order by CustomerSaleID desc)+N'  الى العميل '+(select  top 1 CustomerName from View_CustomersSales order by CustomerSaleID desc)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )

