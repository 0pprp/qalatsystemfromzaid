CREATE proc [dbo].[GetDelegateDataByCity]
@CityID int = NULL
as
select * from Delegates where DelegateState='true' and CityID=@CityID

