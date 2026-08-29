CREATE proc [dbo].[CheckFindBox]
@BoxName nvarchar(255)
as
select * from Boxes where BoxState='true' and BoxName=@BoxName

