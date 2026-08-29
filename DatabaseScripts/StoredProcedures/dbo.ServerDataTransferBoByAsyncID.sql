CREATE proc [dbo].[ServerDataTransferBoByAsyncID]
@AsyncID nvarchar(255) = NULL
as
select top 1 * from TransferBoxs where AsyncID=@AsyncID

