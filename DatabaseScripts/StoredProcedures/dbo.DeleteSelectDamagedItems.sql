 
 CREATE proc [dbo].[DeleteSelectDamagedItems]
 @SelectDamagedItemID int

 as

 delete from SelectDamagedItems
 where SelectDamagedItemID=@SelectDamagedItemID

