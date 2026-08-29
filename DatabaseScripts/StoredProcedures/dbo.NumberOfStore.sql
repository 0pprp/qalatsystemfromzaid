 
CREATE proc [dbo].[NumberOfStore]
as
select count(*) as NumberOfStore from Stores where State='true'


