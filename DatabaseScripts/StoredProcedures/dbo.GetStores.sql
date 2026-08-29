CREATE proc [dbo].[GetStores]
as
select * from View_Stores where State='true'

