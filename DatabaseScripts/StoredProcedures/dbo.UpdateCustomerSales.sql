CREATE proc [dbo].[UpdateCustomerSales]
@CustomerSaleID int = NULL,
@DateCreate datetime,
@DiscountAmountTotal float,
@DiscountAmountTotalDay float,
@UserID int = NULL
as 


INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,N'تم تعديل خصم سعر البيع من '+(select CONVERT(nvarchar(255),DiscountAmountTotalDenar) from View_CustomersSales where CustomerSaleID=@CustomerSaleID)+N' الى '+CONVERT(nvarchar(255),@DiscountAmountTotal*1448)+N' و خصم القسط من '+(select CONVERT(nvarchar(255),DiscountAmountTotalDayDenar) from View_CustomersSales where CustomerSaleID=@CustomerSaleID)+N' الى '+CONVERT(nvarchar(255),@DiscountAmountTotalDay*1448)+N' و تاريخ البيع من '+(select CONVERT(nvarchar(255),DateCreate) from View_CustomersSales where CustomerSaleID=@CustomerSaleID)+N' الى '+CONVERT(nvarchar(255),@DateCreate)+N' للمبيع الخاص بالعميل '+(select CustomerName from View_CustomersSales where CustomerSaleID=@CustomerSaleID)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )

update CustomersSales set 
DiscountAmountTotal=@DiscountAmountTotal ,
DateCreate=@DateCreate,
DateModify=GETUTCDATE(),
DiscountAmountTotalDay=@DiscountAmountTotalDay
where
CustomerSaleID=@CustomerSaleID

