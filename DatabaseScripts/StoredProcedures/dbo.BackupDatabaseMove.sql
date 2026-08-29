

CREATE proc [dbo].[BackupDatabaseMove]
    @DatabaseName sysname
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @BackupPath NVARCHAR(500) = N'C:\Program Files\Microsoft SQL Server\MSSQL17.MSSQLSERVER2025\MSSQL\Backup\';
    DECLARE @BackupFileName NVARCHAR(500);
    DECLARE @DateString NVARCHAR(50);
    DECLARE @BackupLogicalName NVARCHAR(500);

    -- إنشاء سلسلة التاريخ بالتنسيق المطلوب: yyyy-MM-dd hh:mm:ss tt
    -- مثال: 2025-01-01 10:22:22 AM
    SET @DateString = FORMAT(GETDATE(), 'yyyy-MM-dd hh:mm:ss tt');

    -- استبدال النقطتين ":" بشرطة "-" لتكون السلسلة صالحة كجزء من اسم الملف
    SET @DateString = REPLACE(@DateString, ':', '-');

    SET @BackupFileName = @BackupPath + @DatabaseName + ' Move ' + @DateString + '.bak';
    SET @BackupLogicalName = @DatabaseName + ' - Full Backup';

    BEGIN TRY
        BACKUP DATABASE @DatabaseName
        TO DISK = @BackupFileName
        WITH INIT, NAME = @BackupLogicalName;

        PRINT 'تم عمل النسخة الاحتياطية لقاعدة البيانات ' + @DatabaseName + ' بنجاح.';
        PRINT 'ملف النسخة: ' + @BackupFileName;
    END TRY
    BEGIN CATCH
        PRINT 'حدث خطأ أثناء عمل النسخة الاحتياطية لقاعدة البيانات ' + @DatabaseName;
        PRINT ERROR_MESSAGE();
    END CATCH
END



