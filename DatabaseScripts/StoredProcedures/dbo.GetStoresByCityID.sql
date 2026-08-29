 
CREATE proc  [dbo].[GetStoresByCityID]
@CityID int = NULL
as
select * from View_Stores
where  State='true'  and CityID=@CityID

