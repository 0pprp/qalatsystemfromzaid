using BE_Company.IRepository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BE_Company.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BackupController : ControllerBase
    {
        private readonly IBackupDatabaseRepository _backupDatabaseRepository;

        public BackupController(IBackupDatabaseRepository backupDatabaseRepository)
        {
            _backupDatabaseRepository = backupDatabaseRepository;
        }

        [HttpGet("BackupDatabase")]
        public async Task<IActionResult> BackupDatabase()
        {
            var backupFilePath = await _backupDatabaseRepository.BackupDatabase();
            if (string.IsNullOrEmpty(backupFilePath) || !System.IO.File.Exists(backupFilePath))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "Backup file was not created.");
            }
            byte[] fileBytes = await System.IO.File.ReadAllBytesAsync(backupFilePath);
            string fileName = Path.GetFileName(backupFilePath);
            System.IO.File.Delete(backupFilePath);
            return File(fileBytes, "application/octet-stream", fileName);
        }
    }
}
