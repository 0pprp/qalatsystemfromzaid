CREATE proc [dbo].[GetNumberOfDayCustomerZero]
@CustomerID int = NULL
as
DECLARE @StartDate DATE = (select StartDate from View_StartAndEndDate where CustomerID=@CustomerID);
DECLARE @EndDate DATE = (select EndDate from View_StartAndEndDate where CustomerID=@CustomerID);

SELECT ISNULL( DATEDIFF(day, @StartDate, @EndDate),0) AS NumberOfDays;

 

