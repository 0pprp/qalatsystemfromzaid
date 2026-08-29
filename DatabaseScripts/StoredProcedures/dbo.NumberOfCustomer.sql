 
CREATE proc [dbo].[NumberOfCustomer]
as
select count(*) as NumberOfCustomer from Customers where CustomerState='true'


