CREATE proc [dbo].[GetDocumentsByDate]
@FromDate datetime,
@ToDate datetime
as
select * from View_Documents
where CONVERT(date, DocumentDateCreate)>=@FromDate
and 
CONVERT(date, DocumentDateCreate)<=@ToDate

