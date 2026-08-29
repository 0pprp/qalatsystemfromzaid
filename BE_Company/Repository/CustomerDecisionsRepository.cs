using BE_Company.DTO;
using BE_Company.IRepository;
using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;

namespace BE_Company.Repository
{
    public class CustomerDecisionsRepository : ICustomerDecisionsRepository
    {
        private readonly string _connectionString;

        public CustomerDecisionsRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DataBaseConnection")!;
        }

        public async Task<IEnumerable<CustomerWeekPaymentsGetDTO>?> Customers_GetWeakWeekPayers()
        {
            using var connection = new SqlConnection(_connectionString);
            return await connection.QueryAsync<CustomerWeekPaymentsGetDTO>(
                "Customers_GetWeakWeekPayers",
                commandType: CommandType.StoredProcedure);
        }

        public async Task<CustomerWeekDecisionGetDTO?> Customers_PostWeekDecision(int? customerID, int? userID, string? decisionType, string? note)
        {
            using var connection = new SqlConnection(_connectionString);
            return await connection.QueryFirstOrDefaultAsync<CustomerWeekDecisionGetDTO>(
                "Customers_PostWeekDecision",
                new
                {
                    CustomerID = customerID,
                    UserID = userID,
                    DecisionType = decisionType,
                    Note = note
                },
                commandType: CommandType.StoredProcedure);
        }

        public async Task<IEnumerable<CustomerWeekDecisionGetDTO>?> Customers_GetWeekDecisions(string? decisionType, DateTime? fromDate, DateTime? toDate, int? customerID)
        {
            using var connection = new SqlConnection(_connectionString);
            return await connection.QueryAsync<CustomerWeekDecisionGetDTO>(
                "Customers_GetWeekDecisions",
                new
                {
                    DecisionType = decisionType,
                    FromDate = fromDate,
                    ToDate = toDate,
                    CustomerID = customerID
                },
                commandType: CommandType.StoredProcedure);
        }

        public async Task<IEnumerable<DecisionNotificationGetDTO>?> Customers_GetDecisionNotifications()
        {
            using var connection = new SqlConnection(_connectionString);
            return await connection.QueryAsync<DecisionNotificationGetDTO>(
                "Customers_GetDecisionNotifications",
                commandType: CommandType.StoredProcedure);
        }

        public async Task<bool?> Customers_ReadDecisionNotification(int? notificationID)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.ExecuteAsync(
                "Customers_ReadDecisionNotification",
                new { NotificationID = notificationID },
                commandType: CommandType.StoredProcedure);
            return true;
        }

        public async Task<bool?> Customers_ReadAllDecisionNotifications()
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.ExecuteAsync(
                "Customers_ReadAllDecisionNotifications",
                commandType: CommandType.StoredProcedure);
            return true;
        }

        public async Task<IEnumerable<CustomerNoteGetDTO>?> Customers_GetCustomerNotes(int? customerID)
        {
            using var connection = new SqlConnection(_connectionString);
            return await connection.QueryAsync<CustomerNoteGetDTO>(
                "Customers_GetCustomerNotes",
                new { CustomerID = customerID },
                commandType: CommandType.StoredProcedure);
        }

        public async Task<CustomerNoteGetDTO?> Customers_PostCustomerNote(int? customerID, int? userID, string? noteText)
        {
            using var connection = new SqlConnection(_connectionString);
            return await connection.QueryFirstOrDefaultAsync<CustomerNoteGetDTO>(
                "Customers_PostCustomerNote",
                new
                {
                    CustomerID = customerID,
                    UserID = userID,
                    NoteText = noteText
                },
                commandType: CommandType.StoredProcedure);
        }
    }
}
