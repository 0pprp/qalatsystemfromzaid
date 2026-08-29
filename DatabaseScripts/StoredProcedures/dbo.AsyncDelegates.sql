CREATE proc [dbo].[AsyncDelegates]
as
select * from Delegates where AsyncState='false'

