-- v4: the app is a field-agents tool, not a store.
--  * Agents type the name of what they sold: order items are a name + quantity, with no catalog product or price.
--  * The catalog (categories, products, banners) and the commission/payout scheme are removed.

ALTER TABLE agents.OrderItems DROP CONSTRAINT FK_OrderItems_Product;
ALTER TABLE agents.OrderItems DROP COLUMN ProductId, UnitPrice, DailyInstallment, InstallmentDays;
EXEC sp_rename 'agents.OrderItems.ProductName', 'Name', 'COLUMN';
GO

-- The price estimate is gone; the sale total comes from the company system once sold (SaleTotal).
ALTER TABLE agents.Orders ADD CONSTRAINT DF_Orders_EstimatedTotal DEFAULT 0 FOR EstimatedTotal;
GO

DROP TABLE IF EXISTS agents.CommissionEntries;
DROP TABLE IF EXISTS agents.Payouts;
DROP TABLE IF EXISTS agents.CommissionRuns;
DROP TABLE IF EXISTS agents.ProductBranches;
DROP TABLE IF EXISTS agents.ProductSpecs;
DROP TABLE IF EXISTS agents.ProductMedia;
DROP TABLE IF EXISTS agents.Banners;
DROP TABLE IF EXISTS agents.Products;
DROP TABLE IF EXISTS agents.Categories;
GO

-- Settings that belonged to the store / commission scheme.
DELETE FROM agents.AppSettings WHERE [Key] IN (
    'ReferralRatioPercent', 'RoundingUnit', 'HoldDays', 'CreateAccountsUnlockSales', 'MinPayoutAmount',
    'ReferralEnabled', 'AutoReverseOnBranchCancel', 'InstallmentsNotice', 'CommissionsVisible');
