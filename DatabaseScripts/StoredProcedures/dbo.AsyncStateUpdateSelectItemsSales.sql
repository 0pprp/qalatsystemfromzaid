
CREATE proc [dbo].[AsyncStateUpdateSelectItemsSales]
@SelectItemsSaleID int = NULL
as
update SelectItemsSales set AsyncState='true' where SelectItemsSaleID=@SelectItemsSaleID

