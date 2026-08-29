CREATE proc [dbo].[NumberOfCurrentItem]
as
select sum(Quantity) as NumberOfCurrentItem from Items where ItemState='true'

