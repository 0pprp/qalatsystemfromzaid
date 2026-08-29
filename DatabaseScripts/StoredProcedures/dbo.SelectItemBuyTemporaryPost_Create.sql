create proc [dbo].[SelectItemBuyTemporaryPost_Create]
@ItemID int,
@Quantity int,
@UserCreateID int
as
insert into SelectItemBuyTemporary 
(ItemID,Quantity,UserID,AsyncID,AsyncState) values 
(@ItemID,@Quantity,@UserCreateID,NEWID(),0)

