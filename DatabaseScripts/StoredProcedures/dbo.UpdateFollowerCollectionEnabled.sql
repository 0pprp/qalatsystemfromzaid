CREATE proc [dbo].[UpdateFollowerCollectionEnabled]
@DelegateID int = NULL,
@State bit,
@UserID int = NULL
as
BEGIN
    SET NOCOUNT ON;
    IF @DelegateID IS NULL OR @DelegateID <= 0 RETURN;
    IF COL_LENGTH(N'dbo.Delegates', N'FollowerCollectionEnabled') IS NULL RETURN;

    DECLARE @Name NVARCHAR(200);
    DECLARE @Old NVARCHAR(10);
    SELECT TOP 1
        @Name = DelegateName,
        @Old = CONVERT(NVARCHAR(10), ISNULL(FollowerCollectionEnabled, 0))
    FROM dbo.Delegates
    WHERE DelegateID = @DelegateID;

    INSERT INTO [dbo].[Activities]
               ([UserID],[ActivityDescription],[ActivityDate],[AsyncState],[AsyncID])
         VALUES
               (@UserID
               ,N'تم تعديل جباية المتابعين للقائمة '+ISNULL(@Name,N'')+N' من '+ISNULL(@Old,N'0')+N' الى '+CONVERT(NVARCHAR(10),@State)
               ,GETUTCDATE()
               ,'false'
               ,NEWID());

    UPDATE dbo.Delegates SET FollowerCollectionEnabled=@State WHERE DelegateID=@DelegateID;
END
