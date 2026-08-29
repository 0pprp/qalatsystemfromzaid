 
CREATE proc [dbo].[NumberOfBoxes]
as
select count(*) as NumberOfBoxes from  Boxes  where BoxState='true'


