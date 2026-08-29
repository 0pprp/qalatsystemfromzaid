CREATE proc [dbo].[AsyncAddToBox]
as
select * from AddToBox where AsyncState='false'

