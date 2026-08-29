using BE_Company.DTO;

namespace BE_Company.IRepository
{
    public interface IUsersRepository
    {
        Task<UsersGetDTO?> Users_GetUserLoginAdmin(string? userName,string? password);
        Task<UsersGetDTO?> Users_GetUserLogin(string? userName,string? password);
        Task<UsersGetDTO?> Users_GetUserLoginEmployee(string? userName,string? password);
        Task<UsersGetDTO?> Users_Create(UsersPostDTO usersPostDTO);
        Task<UsersGetDTO?> Users_Update(int? userID,UsersPutDTO usersPutDTO);
        Task<bool?> Users_Delete(int? userID,int? userDeleteID);
        Task<IEnumerable<UsersGetDTO>?> Users_GetAll(string? textSearch);
        Task<IEnumerable<ActiveDTO>?> Activities_GetByDate(DateTime? fromDate,DateTime? toDate);
    }
}
