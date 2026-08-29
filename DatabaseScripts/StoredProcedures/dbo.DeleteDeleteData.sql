CREATE proc [dbo].[DeleteDeleteData]
@DeleteDataID int = NULL
as
delete from DeleteData where DeleteDataID=@DeleteDataID

