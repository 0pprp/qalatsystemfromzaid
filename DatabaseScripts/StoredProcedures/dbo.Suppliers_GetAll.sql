CREATE proc [dbo].[Suppliers_GetAll]
@TextSearch nvarchar(100)
as
 SELECT * 
    FROM View_Suppliers 
    WHERE SupplierState = 1 
        AND (@TextSearch IS NULL OR 
             SupplierName LIKE '%' + @TextSearch + '%' OR 
             Address LIKE '%' + @TextSearch + '%' OR 
             PhoneNumber LIKE '%' + @TextSearch + '%');

