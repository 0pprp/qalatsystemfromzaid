CREATE proc [dbo].[AsyncDelegatesTrue]
as
select * from Delegates where AsyncState='true'

