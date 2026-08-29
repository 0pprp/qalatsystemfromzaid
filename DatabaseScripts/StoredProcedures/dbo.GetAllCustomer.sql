 CREATE proc [dbo].[GetAllCustomer]
 as
 SELECT        * FROM           View_Customers where CustomerState='true'

