using Dapper;
using BE_Company.DTO;
using BE_Company.IRepository;
using BE_Company.Sales.Services;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Threading;

namespace BE_Company.Repository
{
    public class UsersRepository : IUsersRepository
    {
        private readonly string _connectionString;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<UsersRepository> _logger;

        public UsersRepository(IConfiguration configuration, IWebHostEnvironment env, ILogger<UsersRepository> logger)
        {
            _connectionString = configuration.GetConnectionString("DataBaseConnection")!;
            _env = env;
            _logger = logger;
        }

        public async Task<UsersGetDTO?> Users_GetUserLoginAdmin(string? userName, string? password)
        {
            var (code, user) = await ResolveLoginAsync(userName, password, UserLoginDiagnostics.IsAdminLoginRole);
            if (code == LoginDiagnosticCode.LoginOk)
            {
                _logger.LogInformation("LoginAdmin succeeded");
            }
            else
            {
                _logger.LogDebug("LoginAdmin failed diagnostic={Code}", code);
            }
            return user;
        }

        public async Task<UsersGetDTO?> Users_GetUserLoginEmployee(string? userName, string? password)
        {
            var (code, user) = await ResolveLoginAsync(userName, password, UserLoginDiagnostics.IsEmployeeLoginRole);
            if (code == LoginDiagnosticCode.LoginOk)
            {
                _logger.LogInformation("LoginEmployee succeeded");
            }
            else
            {
                _logger.LogDebug("LoginEmployee failed diagnostic={Code}", code);
            }
            return user;
        }

        public async Task<UsersGetDTO?> Users_GetUserLogin(string? userName, string? password)
        {
            var (code, user) = await ResolveLoginAsync(userName, password, _ => true);
            _logger.LogDebug("LoginRaw diagnostic={Code}", code);
            return user;
        }

        private async Task<(LoginDiagnosticCode Code, UsersGetDTO? User)> ResolveLoginAsync(
            string? userName,
            string? password,
            Func<string?, bool> roleAllowed)
        {
            var normalized = (userName ?? string.Empty).Trim();
            if (normalized.Length == 0)
            {
                return (LoginDiagnosticCode.UserNotFound, null);
            }

            await using var connection = new SqlConnection(_connectionString);
            var rows = (await connection.QueryAsync(new CommandDefinition(@"
SELECT UserID, UserName, Password, UserType, UserState, UserImage, Email, PhoneNumber, Address
FROM dbo.Users
WHERE LOWER(LTRIM(RTRIM(UserName))) = LOWER(@UserName)
", new { UserName = normalized }))).ToList();

            var candidates = rows.Select(r => new LoginCandidate
            {
                UserId = (int)r.UserID,
                UserName = (string)(r.UserName ?? ""),
                Password = (string)(r.Password ?? ""),
                UserType = (string)(r.UserType ?? ""),
                IsActive = UserLoginDiagnostics.IsActiveUserState(r.UserState)
            }).ToList();

            var (code, match) = UserLoginDiagnostics.Classify(candidates, password, roleAllowed);
            if (code != LoginDiagnosticCode.LoginOk || match is null)
            {
                return (code, null);
            }

            var row = rows.First(r => (int)r.UserID == match.UserId);
            return (code, new UsersGetDTO
            {
                UserID = match.UserId,
                UserName = match.UserName,
                UserType = match.UserType,
                UserImage = row.UserImage as string,
                Email = row.Email as string,
                PhoneNumber = row.PhoneNumber as string,
                Address = row.Address as string,
                // Password intentionally omitted from returned DTO used for JWT.
                Password = null
            });
        }

        private static int _sessionColumnReady;

        public async Task EnsureSalesEmployeeSessionColumnAsync()
        {
            if (Volatile.Read(ref _sessionColumnReady) == 1)
            {
                return;
            }

            using var connection = new SqlConnection(_connectionString);
            await connection.ExecuteAsync(@"
IF COL_LENGTH(N'dbo.Users', N'SessionVersion') IS NULL
    ALTER TABLE dbo.Users ADD SessionVersion INT NOT NULL CONSTRAINT DF_Users_SessionVersion DEFAULT (0);
");
            Volatile.Write(ref _sessionColumnReady, 1);
        }

        public async Task<int> BumpSalesEmployeeSessionVersionAsync(int userId)
        {
            await EnsureSalesEmployeeSessionColumnAsync();
            using var connection = new SqlConnection(_connectionString);
            return await connection.QuerySingleAsync<int>(@"
UPDATE dbo.Users
SET SessionVersion = ISNULL(SessionVersion, 0) + 1
OUTPUT INSERTED.SessionVersion
WHERE UserID = @UserID;
", new { UserID = userId });
        }

        public async Task<int> GetSalesEmployeeSessionVersionAsync(int userId)
        {
            await EnsureSalesEmployeeSessionColumnAsync();
            using var connection = new SqlConnection(_connectionString);
            var version = await connection.ExecuteScalarAsync<int?>(
                "SELECT ISNULL(SessionVersion, 0) FROM dbo.Users WHERE UserID = @UserID",
                new { UserID = userId });
            return version ?? -1;
        }

        public async Task<UsersGetDTO?> Users_Create(UsersPostDTO usersPostDTO)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                string? userImage = null;
                if (usersPostDTO.UserImage != null)
                {
                    userImage = await SaveImage(usersPostDTO.UserImage);
                }
                return await connection.QueryFirstOrDefaultAsync<UsersGetDTO>("Users_Create",
                new
                {
                    UserName = usersPostDTO.UserName?.Trim(),
                    Email = usersPostDTO.Email,
                    Password = usersPostDTO.Password,
                    PhoneNumber = usersPostDTO.PhoneNumber,
                    Address = usersPostDTO.Address,
                    UserCreateID = usersPostDTO.UserCreateID,
                    UserType = usersPostDTO.UserType,
                    UserImage = userImage,
                },
                commandType: CommandType.StoredProcedure);
            }
        }

        public async Task<bool> UserNameExistsAsync(string userName, int? excludeUserId = null, CancellationToken ct = default)
        {
            var normalized = (userName ?? string.Empty).Trim();
            if (normalized.Length == 0)
            {
                return false;
            }

            await using var connection = new SqlConnection(_connectionString);
            var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
SELECT COUNT(1)
FROM dbo.Users
WHERE LOWER(LTRIM(RTRIM(UserName))) = LOWER(@UserName)
  AND (@ExcludeUserId IS NULL OR UserID <> @ExcludeUserId);",
                new { UserName = normalized, ExcludeUserId = excludeUserId },
                cancellationToken: ct));
            return count > 0;
        }

        public async Task<UsersGetDTO?> Users_Update(int? userID, UsersPutDTO usersPutDTO)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var user = await Users_GetByUserID(userID);
                string? userImage = null;
                if (usersPutDTO.UserImage != null)
                {
                    userImage = await SaveImage(usersPutDTO.UserImage);
                }
                else
                {
                    userImage = user?.UserImage;
                }
                return await connection.QueryFirstOrDefaultAsync<UsersGetDTO>("Users_Update",
                new
                {
                    UserID = userID,
                    UserName = usersPutDTO.UserName?.Trim(),
                    Email = usersPutDTO.Email,
                    Password = CheckPasswordValidation(usersPutDTO.Password, user?.Password),
                    PhoneNumber = usersPutDTO.PhoneNumber,
                    Address = usersPutDTO.Address,
                    UserUpdateID = usersPutDTO.UserUpdateID,
                    UserType = usersPutDTO.UserType,
                    UserImage = userImage,
                },
                commandType: CommandType.StoredProcedure);
            }
        }

        private string? CheckPasswordValidation(string? passwordNew, string? passwordOld)
        {
            if (string.IsNullOrEmpty(passwordNew))
            {
                return passwordOld;
            }
            return passwordNew;
        }

        public async Task<UsersGetDTO?> Users_GetByUserID(int? userID)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                return await connection.QuerySingleOrDefaultAsync<UsersGetDTO>("Users_GetByUserID",
                new
                {
                    UserID = userID,
                },
                commandType: CommandType.StoredProcedure);
            }
        }

        public async Task<bool?> Users_Delete(int? userID, int? userDeleteID)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.ExecuteAsync("Users_Delete",
                new
                {
                    UserID = userID,
                    UserDeleteID = userDeleteID
                },
                commandType: CommandType.StoredProcedure);
                return true;
            }
        }

        public async Task<IEnumerable<UsersGetDTO>?> Users_GetAll(string? textSearch)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                return await connection.QueryAsync<UsersGetDTO>("Users_GetAll",
                new
                {
                    TextSearch = textSearch,
                }, 
                commandType: CommandType.StoredProcedure);
            }
        }

        public async Task<IEnumerable<ActiveDTO>?> Activities_GetByDate(DateTime? fromDate, DateTime? toDate)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                return await connection.QueryAsync<ActiveDTO>("Activities_GetByDate",
                new
                {
                    FromDate = fromDate,
                    ToDate = toDate,
                },
                commandType: CommandType.StoredProcedure);
            }
        }
        private async Task<string?> SaveImage(IFormFile? file)
        {
            if (file == null || file.Length == 0)
            {
                return null;
            }

            var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
            var webRootPath = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
            var imagesFolderPath = Path.Combine(webRootPath, "Images");

            if (!Directory.Exists(imagesFolderPath))
            {
                Directory.CreateDirectory(imagesFolderPath);
            }

            var filePath = Path.Combine(imagesFolderPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return fileName;
        }
    }
}
