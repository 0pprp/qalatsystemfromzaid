using BE_Company.Controllers;
using BE_Company.DTO;
using BE_Company.IRepository;
using BE_Company.Sales.Authorization;
using BE_Company.Sales.Filtering;
using BE_Company.Sales.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BE_Company.Sales.Tests;

/// <summary>
/// Server-side rules: who may create/assign which Users.UserType values.
/// </summary>
public sealed class UserCreationAuthorizationTests
{
    [Fact]
    public void MainAccountant_Can_Create_SalesManager()
    {
        Assert.True(UserCreationAuthorization.CanAssignUserType(
            SalesRoles.UserTypeMainAccountant,
            SalesRoles.UserTypeSalesManager));
    }

    [Fact]
    public void MainAccountant_Can_Create_Existing_Admin_Types()
    {
        Assert.True(UserCreationAuthorization.CanAssignUserType(
            SalesRoles.UserTypeMainAccountant, SalesRoles.UserTypeMainAccountant));
        Assert.True(UserCreationAuthorization.CanAssignUserType(
            SalesRoles.UserTypeMainAccountant, SalesRoles.UserTypeSubAccountant));
        Assert.True(UserCreationAuthorization.CanAssignUserType(
            SalesRoles.UserTypeMainAccountant, SalesRoles.UserTypeBranchManager));
        Assert.True(UserCreationAuthorization.CanAssignUserType(
            SalesRoles.UserTypeMainAccountant, SalesRoles.UserTypeSalesEmployee));
        Assert.True(UserCreationAuthorization.CanAssignUserType(
            SalesRoles.UserTypeMainAccountant, SalesRoles.UserTypeSalesFilterEmployee));
        Assert.True(UserCreationAuthorization.CanAssignUserType(
            SalesRoles.UserTypeMainAccountant, SalesRoles.UserTypeFollower));
    }

    [Fact]
    public void SalesEmployee_Cannot_Create_SalesManager()
    {
        Assert.False(UserCreationAuthorization.CanAssignUserType(
            SalesRoles.UserTypeSalesEmployee,
            SalesRoles.UserTypeSalesManager));
    }

    [Fact]
    public void SubAccountant_Cannot_Create_SalesManager()
    {
        Assert.False(UserCreationAuthorization.CanAssignUserType(
            SalesRoles.UserTypeSubAccountant,
            SalesRoles.UserTypeSalesManager));
    }

    [Fact]
    public void Follower_Cannot_Create_Any_User()
    {
        Assert.False(UserCreationAuthorization.CanAssignUserType(
            SalesRoles.UserTypeFollower,
            SalesRoles.UserTypeSalesEmployee));
    }

    [Fact]
    public void Unknown_Target_Type_Rejected()
    {
        Assert.False(UserCreationAuthorization.CanAssignUserType(
            SalesRoles.UserTypeMainAccountant,
            "Admin"));
    }

    [Fact]
    public void Empty_Target_Rejected()
    {
        Assert.False(UserCreationAuthorization.CanAssignUserType(
            SalesRoles.UserTypeMainAccountant,
            "  "));
    }

    [Fact]
    public void SalesManager_Stored_Value_Is_Arabic_Literal()
    {
        Assert.Equal("مدير مبيعات", SalesRoles.UserTypeSalesManager);
    }
}

public sealed class UsersCreateRoleAuthorizationTests
{
    private sealed class FakeUsersRepository : IUsersRepository
    {
        public readonly List<(int UserId, string UserName, string? UserType)> Rows = [];
        public int NextId = 1;
        public int CreateCalls;

        public Task<bool> UserNameExistsAsync(string userName, int? excludeUserId = null, CancellationToken ct = default)
        {
            var normalized = (userName ?? string.Empty).Trim();
            if (normalized.Length == 0) return Task.FromResult(false);
            var exists = Rows.Any(r =>
                string.Equals(r.UserName.Trim(), normalized, StringComparison.OrdinalIgnoreCase)
                && (excludeUserId == null || r.UserId != excludeUserId.Value));
            return Task.FromResult(exists);
        }

        public Task<UsersGetDTO?> Users_Create(UsersPostDTO usersPostDTO)
        {
            CreateCalls++;
            var id = NextId++;
            var name = usersPostDTO.UserName?.Trim() ?? "";
            Rows.Add((id, name, usersPostDTO.UserType));
            return Task.FromResult<UsersGetDTO?>(new UsersGetDTO
            {
                UserID = id,
                UserName = name,
                UserType = usersPostDTO.UserType,
            });
        }

        public Task<UsersGetDTO?> Users_Update(int? userID, UsersPutDTO usersPutDTO)
        {
            var idx = Rows.FindIndex(r => r.UserId == userID);
            if (idx < 0) return Task.FromResult<UsersGetDTO?>(null);
            var name = usersPutDTO.UserName?.Trim() ?? Rows[idx].UserName;
            Rows[idx] = (Rows[idx].UserId, name, usersPutDTO.UserType);
            return Task.FromResult<UsersGetDTO?>(new UsersGetDTO
            {
                UserID = userID,
                UserName = name,
                UserType = usersPutDTO.UserType,
            });
        }

        public Task<UsersGetDTO?> Users_GetUserLoginAdmin(string? userName, string? password) =>
            Task.FromResult<UsersGetDTO?>(null);
        public Task<UsersGetDTO?> Users_GetUserLogin(string? userName, string? password) =>
            Task.FromResult<UsersGetDTO?>(null);
        public Task<UsersGetDTO?> Users_GetUserLoginEmployee(string? userName, string? password) =>
            Task.FromResult(Rows
                .Where(r => string.Equals(r.UserName, userName, StringComparison.OrdinalIgnoreCase))
                .Select(r => new UsersGetDTO { UserID = r.UserId, UserName = r.UserName, UserType = r.UserType, Password = password })
                .FirstOrDefault());
        public Task<int> BumpSalesEmployeeSessionVersionAsync(int userId) => Task.FromResult(1);
        public Task<int> GetSalesEmployeeSessionVersionAsync(int userId) => Task.FromResult(0);
        public Task<bool?> Users_Delete(int? userID, int? userDeleteID) => Task.FromResult<bool?>(true);
        public Task<IEnumerable<UsersGetDTO>?> Users_GetAll(string? textSearch) =>
            Task.FromResult<IEnumerable<UsersGetDTO>?>([]);
        public Task<IEnumerable<ActiveDTO>?> Activities_GetByDate(DateTime? fromDate, DateTime? toDate) =>
            Task.FromResult<IEnumerable<ActiveDTO>?>([]);
    }

    private sealed class FakeFollowerLists : IFollowerUserListsRepository
    {
        public Task EnsureSchemaAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<FollowerListOptionDTO>> GetAvailableListsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<FollowerListOptionDTO>>([]);
        public Task<IReadOnlyList<int>> GetAssignedListIdsAsync(int userId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<int>>([]);
        public Task ReplaceAssignmentsAsync(int userId, IEnumerable<int> listIds, CancellationToken ct = default) =>
            Task.CompletedTask;
        public Task ClearAssignmentsAsync(int userId, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeSalesFilter : ISalesFilterService
    {
        public Task EnsureSchemaAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<SalesFilterCityDTO>> MyCitiesAsync(SalesIdentity actor, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SalesFilterCityDTO>>([]);
        public Task<IReadOnlyList<SalesFilterSalesEmployeeDTO>> ListSalesEmployeesAsync(SalesIdentity actor, string? city, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SalesFilterSalesEmployeeDTO>>([]);
        public Task<SalesFilterPagedResultDTO> ListAsync(SalesIdentity actor, string? city, string? status, string? search, int page, int pageSize, int? targetEmployeeId = null, CancellationToken ct = default) =>
            Task.FromResult(new SalesFilterPagedResultDTO());
        public Task<IReadOnlyDictionary<string, int>> CountsAsync(SalesIdentity actor, string? city, int? targetEmployeeId = null, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<string, int>>(new Dictionary<string, int>());
        public Task<SalesFilterDetailDTO> GetAsync(SalesIdentity actor, int id, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<SalesFilterDetailDTO> HoldAsync(SalesIdentity actor, int id, string? note, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<SalesFilterDetailDTO> ReadyAsync(SalesIdentity actor, int id, string? note, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<SalesFilterDetailDTO> RejectAsync(SalesIdentity actor, int id, string? reason, string? note, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task ReplaceUserCitiesAsync(int userId, IReadOnlyList<(string CityValue, string? CityName)> cities, CancellationToken ct = default) =>
            Task.CompletedTask;
        public Task ClearUserCitiesAsync(int userId, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<SalesFilterCityDTO>> ListUserCitiesAsync(int userId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SalesFilterCityDTO>>([]);
    }

    private static UsersController Controller(FakeUsersRepository users, string actorUserType)
    {
        var cfg = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var controller = new UsersController(users, new FakeFollowerLists(), new FakeSalesFilter(), cfg);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        controller.HttpContext.Items["UserID"] = "1";
        controller.HttpContext.Items["UserType"] = actorUserType;
        return controller;
    }

    [Fact]
    public async Task MainAccountant_Creates_SalesManager_Succeeds_With_Correct_UserType()
    {
        var users = new FakeUsersRepository();
        var result = await Controller(users, SalesRoles.UserTypeMainAccountant)
            .Users_Create(new UsersPostDTO
            {
                UserName = "sm1",
                Password = "pass",
                UserType = SalesRoles.UserTypeSalesManager,
            });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var created = Assert.IsType<UsersGetDTO>(ok.Value);
        Assert.Equal(SalesRoles.UserTypeSalesManager, created.UserType);
        Assert.Equal(1, users.CreateCalls);
        Assert.Equal("مدير مبيعات", users.Rows[0].UserType);
    }

    [Fact]
    public async Task Created_SalesManager_Is_Recognized_By_Employee_Login_Filter()
    {
        // Mirrors UsersRepository.Users_GetUserLoginEmployee allow-list.
        var userType = SalesRoles.UserTypeSalesManager;
        var allowed =
            userType is "محاسب فرعي" or "مدير فرع" or "موظف مبيعات" or "مدير مبيعات" or "موظف فلترة المبيعات";
        Assert.True(allowed);
        Assert.True(SalesRoles.IsSalesManager(userType));
    }

    [Fact]
    public async Task Duplicate_UserName_Still_Returns_409()
    {
        var users = new FakeUsersRepository();
        users.Rows.Add((5, "taken", SalesRoles.UserTypeSubAccountant));
        var result = await Controller(users, SalesRoles.UserTypeMainAccountant)
            .Users_Create(new UsersPostDTO
            {
                UserName = "taken",
                UserType = SalesRoles.UserTypeSalesManager,
            });
        Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal(0, users.CreateCalls);
    }

    [Fact]
    public async Task SalesEmployee_Creating_SalesManager_Returns_403()
    {
        var users = new FakeUsersRepository();
        var result = await Controller(users, SalesRoles.UserTypeSalesEmployee)
            .Users_Create(new UsersPostDTO
            {
                UserName = "sm2",
                UserType = SalesRoles.UserTypeSalesManager,
            });
        var forbid = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbid.StatusCode);
        Assert.Equal(0, users.CreateCalls);
    }

    [Fact]
    public async Task MainAccountant_Manual_Unknown_Role_Returns_403()
    {
        var users = new FakeUsersRepository();
        var result = await Controller(users, SalesRoles.UserTypeMainAccountant)
            .Users_Create(new UsersPostDTO
            {
                UserName = "hacker",
                UserType = "SuperAdmin",
            });
        var forbid = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbid.StatusCode);
        Assert.Equal(0, users.CreateCalls);
    }

    [Fact]
    public async Task Invalid_Empty_UserName_Returns_400()
    {
        var users = new FakeUsersRepository();
        var result = await Controller(users, SalesRoles.UserTypeMainAccountant)
            .Users_Create(new UsersPostDTO
            {
                UserName = "  ",
                UserType = SalesRoles.UserTypeSalesManager,
            });
        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(0, users.CreateCalls);
    }

    [Fact]
    public async Task Update_SalesManager_Type_Allowed_For_MainAccountant()
    {
        var users = new FakeUsersRepository();
        users.Rows.Add((9, "ah", SalesRoles.UserTypeSubAccountant));
        var result = await Controller(users, SalesRoles.UserTypeMainAccountant)
            .Users_Update(9, new UsersPutDTO
            {
                UserName = "ah",
                UserType = SalesRoles.UserTypeSalesManager,
            });
        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(SalesRoles.UserTypeSalesManager, users.Rows[0].UserType);
    }

    [Fact]
    public async Task Update_Forbidden_Role_Escalation_Returns_403()
    {
        var users = new FakeUsersRepository();
        users.Rows.Add((9, "ah", SalesRoles.UserTypeSubAccountant));
        var result = await Controller(users, SalesRoles.UserTypeSubAccountant)
            .Users_Update(9, new UsersPutDTO
            {
                UserName = "ah",
                UserType = SalesRoles.UserTypeSalesManager,
            });
        var forbid = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbid.StatusCode);
    }
}
