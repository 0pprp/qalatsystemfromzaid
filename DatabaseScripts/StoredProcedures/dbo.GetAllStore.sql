CREATE proc [dbo].[GetAllStore]
as
select * from Stores where State='true'

