create proc [dbo].[SelectItemSalesTemporaryPost_Create]
@ItemID int,
@Quantity int,
@UserCreateID int
as
insert into SelectItemSalesTemporary 
(ItemID,Quantity,UserID,AsyncID,AsyncState) values 
(@ItemID,@Quantity,@UserCreateID,NEWID(),0)

