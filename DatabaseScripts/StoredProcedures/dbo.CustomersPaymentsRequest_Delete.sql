CREATE PROC [dbo].[CustomersPaymentsRequest_Delete]
    @CustomersPaymentsRequestID int,
	@UserDeleteID int
AS
BEGIN
   delete from CustomersPaymentsRequest where CustomersPaymentsRequestID = @CustomersPaymentsRequestID
END


