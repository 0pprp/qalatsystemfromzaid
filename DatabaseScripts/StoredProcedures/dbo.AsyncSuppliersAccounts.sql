CREATE proc [dbo].[AsyncSuppliersAccounts]
as
select * from SuppliersAccounts where AsyncState='false'

