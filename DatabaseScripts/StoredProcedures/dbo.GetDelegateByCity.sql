CREATE proc [dbo].[GetDelegateByCity]
@CityID int = NULL
as
select * from View_Delegates where DelegateState='true' and CityID=@CityID

