using BE_Company.Controllers;
using BE_Company.DTO;
using BE_Company.IRepository;
using BE_Company.Sales.Filtering;
using BE_Company.Sales.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BE_Company.Sales.Tests
{
    /// <summary>
    /// Application-level UserName uniqueness (no UNIQUE INDEX yet — production may still have legacy duplicates).
    /// Suggested phase-2: additive UNIQUE filtered index on LOWER(LTRIM(RTRIM(UserName))) after data cleanup.
    /// </summary>
    public sealed class UserNameUniquenessTests
    {
        private sealed class FakeUsersRepository : IUsersRepository
        {
            public readonly List<(int UserId, string UserName)> Rows = [];
            public int NextId = 1;
            public int CreateCalls;
            public int UpdateCalls;

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
                Rows.Add((id, name));
                return Task.FromResult<UsersGetDTO?>(new UsersGetDTO
                {
                    UserID = id,
                    UserName = name,
                    UserType = usersPostDTO.UserType,
                });
            }

            public Task<UsersGetDTO?> Users_Update(int? userID, UsersPutDTO usersPutDTO)
            {
                UpdateCalls++;
                var idx = Rows.FindIndex(r => r.UserId == userID);
                if (idx < 0) return Task.FromResult<UsersGetDTO?>(null);
                var name = usersPutDTO.UserName?.Trim() ?? Rows[idx].UserName;
                Rows[idx] = (Rows[idx].UserId, name);
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
                Task.FromResult<UsersGetDTO?>(null);
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
            public Task<SalesFilterPagedResultDTO> ListAsync(SalesIdentity actor, string? city, string? status, string? search, int page, int pageSize, CancellationToken ct = default) =>
                Task.FromResult(new SalesFilterPagedResultDTO());
            public Task<IReadOnlyDictionary<string, int>> CountsAsync(SalesIdentity actor, string? city, CancellationToken ct = default) =>
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

        private static UsersController Controller(FakeUsersRepository users)
        {
            var cfg = new ConfigurationBuilder().AddInMemoryCollection().Build();
            var controller = new UsersController(users, new FakeFollowerLists(), new FakeSalesFilter(), cfg);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
            controller.HttpContext.Items["UserID"] = "1";
            return controller;
        }

        [Fact]
        public async Task Create_NewUserName_Succeeds()
        {
            var users = new FakeUsersRepository();
            var result = await Controller(users).Users_Create(new UsersPostDTO { UserName = "unique1", UserType = "محاسب فرعي" });
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            Assert.NotNull(ok.Value);
            Assert.Equal(1, users.CreateCalls);
            Assert.Single(users.Rows);
        }

        [Fact]
        public async Task Create_DuplicateUserName_Returns409_AndDoesNotInsert()
        {
            var users = new FakeUsersRepository();
            users.Rows.Add((5, "a"));
            var result = await Controller(users).Users_Create(new UsersPostDTO { UserName = "a", UserType = "محاسب فرعي" });
            var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
            Assert.Equal(409, conflict.StatusCode);
            Assert.Equal(0, users.CreateCalls);
        }

        [Fact]
        public async Task Create_TrimmedDuplicate_Returns409()
        {
            var users = new FakeUsersRepository();
            users.Rows.Add((5, "a"));
            var result = await Controller(users).Users_Create(new UsersPostDTO { UserName = " a ", UserType = "محاسب فرعي" });
            Assert.IsType<ConflictObjectResult>(result.Result);
            Assert.Equal(0, users.CreateCalls);
        }

        [Fact]
        public async Task Create_EmptyUserName_Returns400()
        {
            var users = new FakeUsersRepository();
            var result = await Controller(users).Users_Create(new UsersPostDTO { UserName = "  ", UserType = "محاسب فرعي" });
            Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal(0, users.CreateCalls);
        }

        [Fact]
        public async Task Update_SameUserName_OnSameUser_Succeeds()
        {
            var users = new FakeUsersRepository();
            users.Rows.Add((9, "ah"));
            var result = await Controller(users).Users_Update(9, new UsersPutDTO { UserName = "ah", UserType = "محاسب فرعي" });
            Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(1, users.UpdateCalls);
        }

        [Fact]
        public async Task Update_ToExistingOtherUserName_Returns409()
        {
            var users = new FakeUsersRepository();
            users.Rows.Add((9, "ah"));
            users.Rows.Add((10, "taken"));
            var result = await Controller(users).Users_Update(9, new UsersPutDTO { UserName = "taken", UserType = "محاسب فرعي" });
            Assert.IsType<ConflictObjectResult>(result.Result);
            Assert.Equal(0, users.UpdateCalls);
        }

        [Fact]
        public async Task UserNameExists_IsCaseInsensitive()
        {
            var users = new FakeUsersRepository();
            users.Rows.Add((1, "Alpha"));
            Assert.True(await users.UserNameExistsAsync("alpha"));
            Assert.True(await users.UserNameExistsAsync(" ALPHA "));
            Assert.False(await users.UserNameExistsAsync("alpha", excludeUserId: 1));
        }
    }
}
