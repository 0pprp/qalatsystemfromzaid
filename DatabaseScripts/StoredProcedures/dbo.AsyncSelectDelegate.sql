CREATE proc [dbo].[AsyncSelectDelegate]
as
select * from SelectDelegate where AsyncState='false'

