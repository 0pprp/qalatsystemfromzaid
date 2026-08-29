 
CREATE proc [dbo].[GetMerchantByName]
@MerchantName nvarchar(255)
as
select * from Merchant 
where MerchantName like N'%'+@MerchantName+N'%'

