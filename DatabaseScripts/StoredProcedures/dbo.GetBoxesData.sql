 
CREATE proc [dbo].[GetBoxesData]
as
select * from Boxes where BoxState='true' order by BoxID

