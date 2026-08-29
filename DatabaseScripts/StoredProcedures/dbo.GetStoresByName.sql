CREATE proc [dbo].[GetStoresByName]
@StoreName nvarchar(255)
as
select * from View_Stores
where  State='true' and StoreName like N'%'+@StoreName+N'%'

