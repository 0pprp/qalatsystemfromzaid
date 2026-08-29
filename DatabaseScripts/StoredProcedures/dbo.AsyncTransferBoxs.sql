CREATE proc [dbo].[AsyncTransferBoxs]
as
select * from TransferBoxs where AsyncState='false'

