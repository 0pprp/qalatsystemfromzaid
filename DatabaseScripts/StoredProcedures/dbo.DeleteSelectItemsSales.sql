CREATE proc [dbo].[DeleteSelectItemsSales]
@SelectItemsSaleID int = NULL
as

exec DeleteOneSelectItemsSalesAsyncID @SelectItemsSaleID=@SelectItemsSaleID
delete from SelectItemsSales
where SelectItemsSaleID=@SelectItemsSaleID


 

