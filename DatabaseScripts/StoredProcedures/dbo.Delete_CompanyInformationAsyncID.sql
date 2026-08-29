
CREATE proc [dbo].[Delete_CompanyInformationAsyncID]  @AsyncID nvarchar(255) = null as delete from CompanyInformation where AsyncID=@AsyncID 

