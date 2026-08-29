CREATE proc [dbo].[GetDelegateByDelegateName]
@DelegateName nvarchar(255)
as
select * from View_Delegates
where DelegateName like N'%'+@DelegateName+N'%'

