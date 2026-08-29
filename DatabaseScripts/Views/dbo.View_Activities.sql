create   view [dbo].[View_Activities]
AS
SELECT        dbo.Activities.ActivityID, dbo.Activities.UserID, dbo.Activities.ActivityDescription, dbo.Activities.ActivityDate, dbo.Activities.AsyncState, dbo.Activities.AsyncID, dbo.Users.UserName
FROM            dbo.Activities LEFT OUTER JOIN
                         dbo.Users ON dbo.Activities.UserID = dbo.Users.UserID

