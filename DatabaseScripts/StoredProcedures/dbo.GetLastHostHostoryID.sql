CREATE proc [dbo].[GetLastHostHostoryID]
as

INSERT INTO [dbo].[HostHistory]
           ([AsyncState])
     VALUES
           ('false')
 
select top 1 HostHistoryID from HostHistory order by HostHistoryID desc

