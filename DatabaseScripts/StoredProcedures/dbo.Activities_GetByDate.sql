create proc [dbo].[Activities_GetByDate]
@FromDate datetime ,
@ToDate datetime
as
select * from View_Activities where 
CONVERT(date,ActivityDate)>=CONVERT(date,@FromDate) and 
CONVERT(date,ActivityDate)<= CONVERT(date,@ToDate) 

