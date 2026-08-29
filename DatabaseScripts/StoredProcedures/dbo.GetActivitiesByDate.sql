CREATE proc [dbo].[GetActivitiesByDate]
@FromDate datetime,
@ToDate datetime
as 
select * from View_Activities
where CONVERT(date, ActivityDate)>=@FromDate and CONVERT(date, ActivityDate)<=@ToDate
order by ActivityDate desc

