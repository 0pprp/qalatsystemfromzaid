 
CREATE proc [dbo].[NumberOfCustomersPaymentsRequest]
as
select count(*) as NumberOfCustomersPaymentsRequest from  CustomersPaymentsRequest


