CREATE proc [dbo].[Customers_MoveLegal]
@CustomerID int,
@UserID int
as
declare @IsLegal bit = (select IsLegal from Customers where CustomerID = @CustomerID)
if @IsLegal = 1
begin 
update Customers set IsLegal=0 where CustomerID=@CustomerID
INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,N'تم نقل العميل '+(select CustomerName from Customers where CustomerID=@CustomerID)+' الى القانونية'
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
end
if @IsLegal = 0
begin 
update Customers set IsLegal=1 where CustomerID=@CustomerID
INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,N'تم استعادة العميل '+(select CustomerName from Customers where CustomerID=@CustomerID)+' من القانونية'
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
end
 

