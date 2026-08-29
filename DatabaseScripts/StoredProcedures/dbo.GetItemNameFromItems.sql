CREATE proc [dbo].[GetItemNameFromItems]
@AsyncID nvarchar(255) =null
as
select top 1 ItemName from Items where AsyncID=@AsyncID

