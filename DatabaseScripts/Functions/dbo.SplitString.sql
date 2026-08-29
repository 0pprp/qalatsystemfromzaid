create FUNCTION [dbo].[SplitString]
(
    @Input NVARCHAR(MAX),
    @Delimiter CHAR(1)
)
RETURNS @Output TABLE (Value INT)
AS
BEGIN
    DECLARE @Start INT = 1, @End INT

    WHILE CHARINDEX(@Delimiter, @Input, @Start) > 0
    BEGIN
        SET @End = CHARINDEX(@Delimiter, @Input, @Start)
        INSERT INTO @Output (Value)
        VALUES (CAST(SUBSTRING(@Input, @Start, @End - @Start) AS INT))
        SET @Start = @End + 1
    END

    INSERT INTO @Output (Value)
    VALUES (CAST(SUBSTRING(@Input, @Start, LEN(@Input) - @Start + 1) AS INT))

    RETURN
END

