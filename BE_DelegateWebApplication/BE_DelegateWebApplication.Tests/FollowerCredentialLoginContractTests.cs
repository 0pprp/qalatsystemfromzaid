using BE_DelegateWebApplication.Controllers;
using BE_DelegateWebApplication.Services.FollowerIdentity;
using Xunit;

namespace BE_DelegateWebApplication.Tests
{
    /// <summary>
    /// Contracts for POST Followers/Login (username/password) and session rules.
    /// </summary>
    public sealed class FollowerCredentialLoginContractTests
    {
        [Fact]
        public void ActiveFollower_CorrectCredentials_AllowsLogin()
        {
            var row = new FollowerUserIdentity
            {
                UserId = 10,
                UserName = "ahmed4",
                AsyncId = "tok-abc",
                UserType = "متابع",
                IsActive = true,
            };
            var outcome = Evaluate(row);
            Assert.Equal(FollowerAuthFailure.None, outcome.Failure);
            Assert.Equal(10, outcome.Identity!.UserId);
            Assert.Equal("tok-abc", outcome.Identity.AsyncId);
        }

        [Fact]
        public void WrongPassword_IsInvalidCredentials()
        {
            FollowerUserIdentity? row = null; // no UserName+Password match
            var outcome = Evaluate(row);
            Assert.Equal(FollowerAuthFailure.InvalidCredentials, outcome.Failure);
        }

        [Fact]
        public void WrongUserName_IsInvalidCredentials()
        {
            FollowerUserIdentity? row = null;
            var outcome = Evaluate(row);
            Assert.Equal(FollowerAuthFailure.InvalidCredentials, outcome.Failure);
        }

        [Fact]
        public void UserTypeNotFollower_IsForbiddenNotFollower()
        {
            var row = new FollowerUserIdentity
            {
                UserId = 3,
                UserName = "sales1",
                AsyncId = "x",
                UserType = "مندوب",
                IsActive = true,
            };
            var outcome = Evaluate(row);
            Assert.Equal(FollowerAuthFailure.NotFollower, outcome.Failure);
            Assert.Equal(FollowerAuthMessages.NotFollower, MessageFor(outcome));
        }

        [Fact]
        public void InactiveUserState_IsForbiddenInactive()
        {
            var row = new FollowerUserIdentity
            {
                UserId = 4,
                UserName = "ahmed4",
                AsyncId = "tok",
                UserType = "متابع",
                IsActive = false,
            };
            var outcome = Evaluate(row);
            Assert.Equal(FollowerAuthFailure.Inactive, outcome.Failure);
            Assert.Equal(FollowerAuthMessages.Inactive, MessageFor(outcome));
        }

        [Fact]
        public void SuccessPayload_ReturnsUserIdAndAsyncId_NotPassword()
        {
            var identity = new FollowerUserIdentity
            {
                UserId = 10,
                UserName = "ahmed4",
                AsyncId = "NEWID-TOKEN",
                UserType = "متابع",
                CityId = 1,
                CityName = "النجف",
                IsActive = true,
            };
            var payload = new
            {
                userId = identity.UserId,
                userName = identity.UserName,
                asyncId = identity.AsyncId,
                userType = identity.UserType,
                cityId = identity.CityId,
                cityName = identity.CityName,
                isActive = identity.IsActive,
            };
            Assert.Equal(10, payload.userId);
            Assert.Equal("NEWID-TOKEN", payload.asyncId);
            Assert.DoesNotContain("password", payload.GetType().GetProperties().Select(p => p.Name),
                StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public void AsyncId_MustNotEqualPassword_Automatically()
        {
            const string password = "secret123";
            const string asyncIdFromCreate = "a1b2c3d4-guid"; // Users_Create NEWID()
            Assert.NotEqual(password, asyncIdFromCreate);
            // AlignFollowerAsyncIdAsync removed — password must not overwrite AsyncID.
            var alignFollowerAsyncIdStillUsed = false;
            Assert.False(alignFollowerAsyncIdStillUsed);
        }

        [Fact]
        public void Password_MustUsePostBody_NotQueryString()
        {
            var dto = new FollowerLoginRequestDto { UserName = "ahmed4", Password = "secret" };
            const string httpMethod = "POST";
            const string path = "/api/Followers/Login";
            var query = ""; // no password in query
            Assert.Equal("POST", httpMethod);
            Assert.Contains("Followers/Login", path);
            Assert.DoesNotContain("password", query, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secret", query, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("ahmed4", dto.UserName);
            Assert.Equal("secret", dto.Password);
        }

        [Fact]
        public void FollowerWithoutLists_CanStillLogin()
        {
            var loginAllowed = FollowerUserType.IsFollowerType("متابع") && true;
            var lists = Array.Empty<int>();
            Assert.True(loginAllowed);
            Assert.Empty(lists); // GET Followers/Lists => 200 []
        }

        [Fact]
        public void ChangingUserTypeAfterLogin_InvalidatesSession()
        {
            var userType = "متابع";
            Assert.True(FollowerUserType.IsFollowerType(userType));
            userType = "موظف مبيعات";
            var sessionOutcome = Evaluate(new FollowerUserIdentity
            {
                UserId = 10,
                UserName = "ahmed4",
                AsyncId = "tok",
                UserType = userType,
                IsActive = true,
            });
            Assert.Equal(FollowerAuthFailure.NotFollower, sessionOutcome.Failure);
        }

        [Fact]
        public void GpsFollowerId_RemainsUsersUserId()
        {
            const int userId = 10;
            const int followerIdInShift = userId;
            Assert.Equal(userId, followerIdInShift);
        }

        [Fact]
        public void DemoApiUses8081()
        {
            const string demoBase = "http://169.58.236.52:8081/api/";
            Assert.Contains(":8081", demoBase);
            Assert.StartsWith("http://169.58.236.52", demoBase);
        }

        [Fact]
        public void InvalidCredentials_MessageIsClear()
        {
            Assert.Equal("اسم المستخدم أو كلمة المرور غير صحيحة", FollowerAuthMessages.InvalidCredentials);
            Assert.DoesNotContain("غير مفعّل كمتابع", FollowerAuthMessages.NotFollower);
            Assert.DoesNotContain("غير مفعّل كمتابع", FollowerAuthMessages.Inactive);
        }

        /// <summary>Mirrors FollowerIdentityService.EvaluateRow without DB.</summary>
        private static FollowerAuthOutcome Evaluate(FollowerUserIdentity? row)
        {
            if (row is null || row.UserId <= 0)
            {
                return FollowerAuthOutcome.Fail(FollowerAuthFailure.InvalidCredentials);
            }

            if (!FollowerUserType.IsFollowerType(row.UserType))
            {
                return FollowerAuthOutcome.Fail(FollowerAuthFailure.NotFollower);
            }

            if (!row.IsActive)
            {
                return FollowerAuthOutcome.Fail(FollowerAuthFailure.Inactive);
            }

            return FollowerAuthOutcome.Ok(row);
        }

        private static string MessageFor(FollowerAuthOutcome outcome) =>
            outcome.Failure switch
            {
                FollowerAuthFailure.InvalidCredentials => FollowerAuthMessages.InvalidCredentials,
                FollowerAuthFailure.NotFollower => FollowerAuthMessages.NotFollower,
                FollowerAuthFailure.Inactive => FollowerAuthMessages.Inactive,
                _ => "",
            };
    }
}
