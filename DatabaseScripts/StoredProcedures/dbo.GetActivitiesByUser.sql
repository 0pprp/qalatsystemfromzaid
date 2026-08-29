CREATE proc [dbo].[GetActivitiesByUser]
@FromDate datetime,
@ToDate datetime,
@UserId int = NULL
as 
select * from View_Activities
where CONVERT(date, ActivityDate)>=@FromDate and CONVERT(date, ActivityDate)<=@ToDate and UserID=@UserId order by ActivityDate desc

