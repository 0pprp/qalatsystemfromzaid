namespace BE_Company.IRepository
{
    public interface IBackupDatabaseRepository
    {
        Task<string?> BackupDatabase();
    }
}
