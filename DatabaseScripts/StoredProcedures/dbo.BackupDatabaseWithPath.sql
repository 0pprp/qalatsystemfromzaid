

CREATE proc [dbo].[BackupDatabaseWithPath]
    @DatabaseName sysname,
    @BackupFilePath NVARCHAR(500) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        DECLARE @BackupPath NVARCHAR(500) = N'C:\Program Files\Microsoft SQL Server\MSSQL17.MSSQLSERVER2025\MSSQL\Backup\';
        DECLARE @BackupFileName NVARCHAR(500);
        DECLARE @DateString NVARCHAR(50);
        DECLARE @BackupLogicalName NVARCHAR(500);
        SET @DateString = FORMAT(GETDATE(), 'yyyy-MM-dd hh:mm:ss tt');
        SET @DateString = REPLACE(@DateString, ':', '-');
        SET @BackupFileName = @BackupPath + @DatabaseName + ' ' + @DateString + '.bak';
        SET @BackupLogicalName = @DatabaseName + ' - Full Backup';
        BACKUP DATABASE @DatabaseName
        TO DISK = @BackupFileName
        WITH INIT, NAME = @BackupLogicalName;
        SET @BackupFilePath = @BackupFileName;
    END TRY
    BEGIN CATCH
        SET @BackupFilePath = NULL;
        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
        RAISERROR (@ErrorMessage, 16, 1);
    END CATCH
END
