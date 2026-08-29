using BE_Company.DTO;

namespace BE_Company.IRepository
{
    public interface ICustomerDecisionsRepository
    {
        Task<IEnumerable<CustomerWeekPaymentsGetDTO>?> Customers_GetWeakWeekPayers();
        Task<CustomerWeekDecisionGetDTO?> Customers_PostWeekDecision(int? customerID, int? userID, string? decisionType, string? note);
        Task<IEnumerable<CustomerWeekDecisionGetDTO>?> Customers_GetWeekDecisions(string? decisionType, DateTime? fromDate, DateTime? toDate, int? customerID);
        Task<IEnumerable<DecisionNotificationGetDTO>?> Customers_GetDecisionNotifications();
        Task<bool?> Customers_ReadDecisionNotification(int? notificationID);
        Task<bool?> Customers_ReadAllDecisionNotifications();
        Task<IEnumerable<CustomerNoteGetDTO>?> Customers_GetCustomerNotes(int? customerID);
        Task<CustomerNoteGetDTO?> Customers_PostCustomerNote(int? customerID, int? userID, string? noteText);
    }
}
