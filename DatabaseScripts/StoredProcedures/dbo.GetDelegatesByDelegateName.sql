CREATE proc [dbo].[GetDelegatesByDelegateName]
@DelegateName nvarchar(255)
as
select * from View_Delegates where DelegateState='true' and 
DelegateName like N'%'+@DelegateName+N'%'

