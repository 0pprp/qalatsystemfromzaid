CREATE proc [dbo].[CheckStoreFind]
@StoreName nvarchar(255)
as
select * from Stores where State='true' and StoreName=@StoreName

