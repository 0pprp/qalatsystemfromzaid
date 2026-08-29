CREATE proc [dbo].[GetItems]
as
SELECT     * FROM      View_Items
where ItemState='true'

