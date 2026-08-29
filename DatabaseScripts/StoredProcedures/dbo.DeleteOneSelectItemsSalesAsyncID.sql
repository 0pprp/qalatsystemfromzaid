CREATE proc [dbo].[DeleteOneSelectItemsSalesAsyncID]
@SelectItemsSaleID int = NULL
as
insert into DeleteData (SelectItemsSalesAsyncID) values ((select AsyncID from SelectItemsSales where SelectItemsSaleID=@SelectItemsSaleID))

