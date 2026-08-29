CREATE proc [dbo].[AsyncSelectItemsSales]
as
select * from SelectItemsSales where AsyncState='false'

