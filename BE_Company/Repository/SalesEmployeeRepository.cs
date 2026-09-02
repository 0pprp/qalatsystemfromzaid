using BE_Company.DTO;
using BE_Company.IRepository;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_Company.Repository
{
    public class SalesEmployeeRepository : ISalesEmployeeRepository
    {
        private readonly string _connectionString;

        public SalesEmployeeRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DataBaseConnection")!;
        }

        public async Task EnsureTablesAsync()
        {
            const string sql = @"
IF OBJECT_ID('dbo.SalesEmployeeShifts', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalesEmployeeShifts (
        ShiftID INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        UserID INT NOT NULL,
        ShiftDate DATE NOT NULL,
        StartedAt DATETIME NOT NULL,
        EndsAt DATETIME NOT NULL
    );
    CREATE UNIQUE INDEX UX_SalesEmployeeShifts_UserDate
        ON dbo.SalesEmployeeShifts (UserID, ShiftDate);
END

IF OBJECT_ID('dbo.SalesEmployeeTrackPoints', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalesEmployeeTrackPoints (
        PointID INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ShiftID INT NOT NULL,
        UserID INT NOT NULL,
        RecordedAt DATETIME NOT NULL,
        Latitude FLOAT NOT NULL,
        Longitude FLOAT NOT NULL,
        Accuracy FLOAT NULL,
        ClientKey NVARCHAR(64) NULL,
        CreatedAt DATETIME NOT NULL CONSTRAINT DF_SalesEmployeeTrackPoints_CreatedAt DEFAULT (GETDATE())
    );
    CREATE UNIQUE INDEX UX_SalesEmployeeTrackPoints_ClientKey
        ON dbo.SalesEmployeeTrackPoints (UserID, ClientKey)
        WHERE ClientKey IS NOT NULL;
END

IF OBJECT_ID('dbo.SalesCustomerRatings', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalesCustomerRatings (
        RatingID INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        UserID INT NOT NULL,
        CustomerID INT NULL,
        CustomerName NVARCHAR(200) NULL,
        PhoneNumber NVARCHAR(50) NULL,
        RatingLevel INT NOT NULL,
        Notes NVARCHAR(MAX) NULL,
        RejectionReason NVARCHAR(MAX) NULL,
        CreatedAt DATETIME NOT NULL CONSTRAINT DF_SalesCustomerRatings_CreatedAt DEFAULT (GETDATE())
    );
END";
            using var connection = new SqlConnection(_connectionString);
            await connection.ExecuteAsync(sql);
        }

        public async Task<SalesEmployeeShiftDTO?> GetActiveShift(int userID, DateTime nowIraq)
        {
            await EnsureTablesAsync();
            var shiftDate = ShiftDateOf(nowIraq);
            using var connection = new SqlConnection(_connectionString);
            var row = await connection.QueryFirstOrDefaultAsync<SalesEmployeeShiftDTO>(
                @"SELECT ShiftID, UserID, ShiftDate, StartedAt, EndsAt
                  FROM dbo.SalesEmployeeShifts
                  WHERE UserID = @UserID AND ShiftDate = @ShiftDate",
                new { UserID = userID, ShiftDate = shiftDate });
            if (row == null)
            {
                return null;
            }
            row.Active = nowIraq < row.EndsAt;
            return row;
        }

        public async Task<SalesEmployeeShiftDTO> StartShift(int userID, DateTime nowIraq)
        {
            await EnsureTablesAsync();
            var existing = await GetActiveShift(userID, nowIraq);
            if (existing != null)
            {
                existing.Active = true;
                return existing;
            }

            var shiftDate = ShiftDateOf(nowIraq);
            var endsAt = shiftDate.Date.AddDays(1).AddHours(3);
            using var connection = new SqlConnection(_connectionString);
            var row = await connection.QueryFirstAsync<SalesEmployeeShiftDTO>(
                @"INSERT INTO dbo.SalesEmployeeShifts (UserID, ShiftDate, StartedAt, EndsAt)
                  OUTPUT INSERTED.ShiftID, INSERTED.UserID, INSERTED.ShiftDate, INSERTED.StartedAt, INSERTED.EndsAt
                  VALUES (@UserID, @ShiftDate, @StartedAt, @EndsAt)",
                new
                {
                    UserID = userID,
                    ShiftDate = shiftDate.Date,
                    StartedAt = nowIraq,
                    EndsAt = endsAt
                });
            row.Active = true;
            return row;
        }

        public async Task<SalesEmployeeTrackSyncResultDTO> SyncTrackPoints(int userID, SalesEmployeeTrackSyncDTO dto)
        {
            await EnsureTablesAsync();
            var result = new SalesEmployeeTrackSyncResultDTO { ShiftID = dto.ShiftID };
            if (dto.Points == null || dto.Points.Count == 0)
            {
                return result;
            }

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            if (!result.ShiftID.HasValue || result.ShiftID.Value <= 0)
            {
                var shiftDate = ShiftDateOf(IraqNow());
                result.ShiftID = await connection.QueryFirstOrDefaultAsync<int?>(
                    @"SELECT TOP 1 ShiftID FROM dbo.SalesEmployeeShifts
                      WHERE UserID = @UserID AND ShiftDate = @ShiftDate",
                    new { UserID = userID, ShiftDate = shiftDate });
            }

            if (!result.ShiftID.HasValue)
            {
                throw new InvalidOperationException("لا توجد وردية نشطة لمزامنة المسار");
            }

            foreach (var point in dto.Points)
            {
                try
                {
                    var inserted = await connection.ExecuteAsync(
                        @"INSERT INTO dbo.SalesEmployeeTrackPoints
                          (ShiftID, UserID, RecordedAt, Latitude, Longitude, Accuracy, ClientKey)
                          VALUES (@ShiftID, @UserID, @RecordedAt, @Latitude, @Longitude, @Accuracy, @ClientKey)",
                        new
                        {
                            ShiftID = result.ShiftID,
                            UserID = userID,
                            RecordedAt = point.RecordedAt,
                            point.Latitude,
                            point.Longitude,
                            point.Accuracy,
                            ClientKey = string.IsNullOrWhiteSpace(point.ClientKey) ? null : point.ClientKey
                        });
                    result.Inserted += inserted;
                }
                catch (SqlException ex) when (ex.Number == 2601 || ex.Number == 2627)
                {
                    result.Skipped++;
                }
            }

            return result;
        }

        public async Task<SalesCustomerRatingGetDTO?> SaveRating(int userID, SalesCustomerRatingPostDTO dto)
        {
            await EnsureTablesAsync();
            using var connection = new SqlConnection(_connectionString);
            return await connection.QueryFirstOrDefaultAsync<SalesCustomerRatingGetDTO>(
                @"INSERT INTO dbo.SalesCustomerRatings
                  (UserID, CustomerID, CustomerName, PhoneNumber, RatingLevel, Notes, RejectionReason)
                  OUTPUT INSERTED.RatingID, INSERTED.UserID, INSERTED.CustomerID, INSERTED.CustomerName,
                         INSERTED.PhoneNumber, INSERTED.RatingLevel, INSERTED.Notes, INSERTED.RejectionReason, INSERTED.CreatedAt
                  VALUES (@UserID, @CustomerID, @CustomerName, @PhoneNumber, @RatingLevel, @Notes, @RejectionReason)",
                new
                {
                    UserID = userID,
                    dto.CustomerID,
                    dto.CustomerName,
                    dto.PhoneNumber,
                    dto.RatingLevel,
                    dto.Notes,
                    dto.RejectionReason
                });
        }

        public async Task<IEnumerable<SalesEmployeeSearchCustomerDTO>> SearchCustomers(string? textSearch)
        {
            using var connection = new SqlConnection(_connectionString);
            var rows = await connection.QueryAsync<CustomersGetDTO>(
                "Customers_GetAll",
                new
                {
                    DelegateID = (int?)null,
                    TextSearch = string.IsNullOrWhiteSpace(textSearch) || textSearch == "null" ? null : textSearch,
                    ShowType = "الجميع"
                },
                commandType: System.Data.CommandType.StoredProcedure);

            return rows.Select(c => new SalesEmployeeSearchCustomerDTO
            {
                CustomerID = c.CustomerID,
                CustomerName = c.CustomerName,
                PhoneNumber = c.PhoneNumber,
                Address = c.Address,
                ShopName = c.ShopName,
                NearestFunctionPoint = c.NearestFunctionPoint,
                SaleName = c.SaleName,
                ReceiptName = c.ReceiptName,
                DelegateID = c.DelegateID,
                DelegateName = c.DelegateName,
                CityName = c.CityName
            });
        }

        public static DateTime IraqNow()
        {
            TimeZoneInfo tz;
            try
            {
                tz = TimeZoneInfo.FindSystemTimeZoneById("Arabic Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                tz = TimeZoneInfo.FindSystemTimeZoneById("Asia/Baghdad");
            }
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        }

        public static DateTime ShiftDateOf(DateTime iraqNow)
        {
            var date = iraqNow.Date;
            return iraqNow.Hour < 3 ? date.AddDays(-1) : date;
        }
    }
}
