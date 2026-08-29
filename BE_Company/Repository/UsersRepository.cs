using Dapper;
using BE_Company.DTO;
using BE_Company.IRepository;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace BE_Company.Repository
{
    public class UsersRepository : IUsersRepository
    {
        private readonly string _connectionString;
        private readonly IWebHostEnvironment _env;

        public UsersRepository(IConfiguration configuration, IWebHostEnvironment env)
        {
            _connectionString = configuration.GetConnectionString("DataBaseConnection")!;
            _env = env;
        }

        public async Task<UsersGetDTO?> Users_GetUserLoginAdmin(string? userName, string? password)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QueryFirstOrDefaultAsync<UsersGetDTO>("Users_GetUserLogin",
                new
                {
                    UserName = userName,
                    Password = password
                },
                commandType: CommandType.StoredProcedure);
                if (result != null)
                {
                    if (result.UserType == "محاسب رئيسي" || result.UserType == "مدير فرع")
                    {
                        return result;
                    }
                }
                return null;
            }
        }

        public async Task<UsersGetDTO?> Users_GetUserLoginEmployee(string? userName, string? password)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result =  await connection.QueryFirstOrDefaultAsync<UsersGetDTO>("Users_GetUserLogin",
                new
                {
                    UserName = userName,
                    Password = password
                },
                commandType: CommandType.StoredProcedure);
                if (result != null)
                {
                    if (result.UserType == "محاسب فرعي" || result.UserType == "مدير فرع")
                    {
                        return result;
                    }
                }
                return null;
            }
        }

        public async Task<UsersGetDTO?> Users_GetUserLogin(string? userName, string? password)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = await connection.QueryFirstOrDefaultAsync<UsersGetDTO>("Users_GetUserLogin",
                new
                {
                    UserName = userName,
                    Password = password
                },
                commandType: CommandType.StoredProcedure);
                return result;
            }
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
                    UserName = usersPostDTO.UserName,
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
                    UserName = usersPutDTO.UserName,
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
