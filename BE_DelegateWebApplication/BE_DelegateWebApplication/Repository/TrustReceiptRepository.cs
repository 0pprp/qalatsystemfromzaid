using BE_DelegateWebApplication.DTO;
using BE_DelegateWebApplication.IRepository;
using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;

namespace BE_DelegateWebApplication.Repository
{
    public class TrustReceiptRepository : ITrustReceiptRepository
    {
        private readonly IConfiguration _configuration;

        public TrustReceiptRepository(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private IDbConnection Connection => new SqlConnection(_configuration.GetConnectionString("DataBaseConnection"));

        public async Task<int> CreateAsync(TrustReceiptDTO dto)
        {
            using var connection = Connection;
            var parameters = new DynamicParameters();
            parameters.Add("@ContractNumber", dto.ContractNumber);
            parameters.Add("@ContractDate", dto.ContractDate);
            parameters.Add("@ContractType", dto.ContractType);
            parameters.Add("@ContractNotes", dto.ContractNotes);
            parameters.Add("@ContractStatus", dto.ContractStatus);
            parameters.Add("@FirstPartyName", dto.FirstPartyName);
            parameters.Add("@CompanyType", dto.CompanyType);
            parameters.Add("@CompanyRepresentativeName", dto.CompanyRepresentativeName);
            parameters.Add("@CompanyRepresentativeRole", dto.CompanyRepresentativeRole);
            parameters.Add("@BuyerID", dto.BuyerID);
            parameters.Add("@BuyerName", dto.BuyerName);
            parameters.Add("@BuyerNationalCardNumber", dto.BuyerNationalCardNumber);
            parameters.Add("@BuyerGovernorate", dto.BuyerGovernorate);
            parameters.Add("@BuyerWorkOrResidenceAddress", dto.BuyerWorkOrResidenceAddress);
            parameters.Add("@BuyerNearestLandmark", dto.BuyerNearestLandmark);
            parameters.Add("@BuyerPhoneNumber", dto.BuyerPhoneNumber);
            parameters.Add("@BuyerRationCenterNumber", dto.BuyerRationCenterNumber);
            parameters.Add("@BuyerAffiliation", dto.BuyerAffiliation);
            parameters.Add("@BuyerMukhtarName", dto.BuyerMukhtarName);
            parameters.Add("@ProductID", dto.ProductID);
            parameters.Add("@ProductType", dto.ProductType);
            parameters.Add("@ProductName", dto.ProductName);
            parameters.Add("@ProductDescription", dto.ProductDescription);
            parameters.Add("@ProductNumber", dto.ProductNumber);
            parameters.Add("@ProductDeliveryCondition", dto.ProductDeliveryCondition);
            parameters.Add("@TotalAmountNumber", dto.TotalAmountNumber);
            parameters.Add("@TotalAmountText", dto.TotalAmountText);
            parameters.Add("@FirstInstallmentAmount", dto.FirstInstallmentAmount);
            parameters.Add("@FirstInstallmentDate", dto.FirstInstallmentDate);
            parameters.Add("@InstallmentsCount", dto.InstallmentsCount);
            parameters.Add("@InstallmentAmount", dto.InstallmentAmount);
            parameters.Add("@InstallmentsStartDate", dto.InstallmentsStartDate);
            parameters.Add("@InstallmentsEndDate", dto.InstallmentsEndDate);
            parameters.Add("@RemainingAmount", dto.RemainingAmount);
            parameters.Add("@PaymentMethod", dto.PaymentMethod);
            parameters.Add("@ProductDeliveryDate", dto.ProductDeliveryDate);
            parameters.Add("@DeliveryPlace", dto.DeliveryPlace);
            parameters.Add("@IsProductInspected", dto.IsProductInspected);
            parameters.Add("@InspectionNotes", dto.InspectionNotes);
            parameters.Add("@IsReceivedByBuyer", dto.IsReceivedByBuyer);
            parameters.Add("@TrustReceiptNumber", dto.TrustReceiptNumber);
            parameters.Add("@TrustReceiptDate", dto.TrustReceiptDate);
            parameters.Add("@ReceiptAmountNumber", dto.ReceiptAmountNumber);
            parameters.Add("@ReceiptAmountText", dto.ReceiptAmountText);
            parameters.Add("@ReceiverName", dto.ReceiverName);
            parameters.Add("@DelivererName", dto.DelivererName);
            parameters.Add("@DeliveryReason", dto.DeliveryReason);
            parameters.Add("@IdentityDocumentNumber", dto.IdentityDocumentNumber);
            parameters.Add("@Address", dto.Address);
            parameters.Add("@PhoneNumber", dto.PhoneNumber);
            parameters.Add("@ReceiverSignature", dto.ReceiverSignature);
            parameters.Add("@DelivererSignature", dto.DelivererSignature);
            parameters.Add("@FirstWitnessName", dto.FirstWitnessName);
            parameters.Add("@FirstWitnessSignature", dto.FirstWitnessSignature);
            parameters.Add("@SecondWitnessName", dto.SecondWitnessName);
            parameters.Add("@SecondWitnessSignature", dto.SecondWitnessSignature);
            parameters.Add("@FirstPartySignature", dto.FirstPartySignature);
            parameters.Add("@SecondPartySignature", dto.SecondPartySignature);
            parameters.Add("@CashierSignature", dto.CashierSignature);
            parameters.Add("@SalesRepresentativeSignature", dto.SalesRepresentativeSignature);
            parameters.Add("@SalesRepresentativeName", dto.SalesRepresentativeName);
            parameters.Add("@CashierName", dto.CashierName);
            parameters.Add("@CreatedByUserID", dto.CreatedByUserID);
            parameters.Add("@DelegateID", dto.DelegateID);
            parameters.Add("@IsActive", dto.IsActive ?? true);
            parameters.Add("@TrustReceiptID", dbType: DbType.Int32, direction: ParameterDirection.Output);

            await connection.ExecuteAsync("TrustReceipts_Create", parameters, commandType: CommandType.StoredProcedure);
            return parameters.Get<int>("@TrustReceiptID");
        }

        public async Task<bool> UpdateAsync(TrustReceiptDTO dto)
        {
            using var connection = Connection;
            var p = new DynamicParameters();
            p.Add("@TrustReceiptID", dto.TrustReceiptID);
            p.Add("@ContractNumber", dto.ContractNumber);
            p.Add("@ContractDate", dto.ContractDate);
            p.Add("@ContractType", dto.ContractType);
            p.Add("@ContractNotes", dto.ContractNotes);
            p.Add("@ContractStatus", dto.ContractStatus);
            p.Add("@FirstPartyName", dto.FirstPartyName);
            p.Add("@CompanyType", dto.CompanyType);
            p.Add("@CompanyRepresentativeName", dto.CompanyRepresentativeName);
            p.Add("@CompanyRepresentativeRole", dto.CompanyRepresentativeRole);
            p.Add("@BuyerID", dto.BuyerID);
            p.Add("@BuyerName", dto.BuyerName);
            p.Add("@BuyerNationalCardNumber", dto.BuyerNationalCardNumber);
            p.Add("@BuyerGovernorate", dto.BuyerGovernorate);
            p.Add("@BuyerWorkOrResidenceAddress", dto.BuyerWorkOrResidenceAddress);
            p.Add("@BuyerNearestLandmark", dto.BuyerNearestLandmark);
            p.Add("@BuyerPhoneNumber", dto.BuyerPhoneNumber);
            p.Add("@BuyerRationCenterNumber", dto.BuyerRationCenterNumber);
            p.Add("@BuyerAffiliation", dto.BuyerAffiliation);
            p.Add("@BuyerMukhtarName", dto.BuyerMukhtarName);
            p.Add("@ProductID", dto.ProductID);
            p.Add("@ProductType", dto.ProductType);
            p.Add("@ProductName", dto.ProductName);
            p.Add("@ProductDescription", dto.ProductDescription);
            p.Add("@ProductNumber", dto.ProductNumber);
            p.Add("@ProductDeliveryCondition", dto.ProductDeliveryCondition);
            p.Add("@TotalAmountNumber", dto.TotalAmountNumber);
            p.Add("@TotalAmountText", dto.TotalAmountText);
            p.Add("@FirstInstallmentAmount", dto.FirstInstallmentAmount);
            p.Add("@FirstInstallmentDate", dto.FirstInstallmentDate);
            p.Add("@InstallmentsCount", dto.InstallmentsCount);
            p.Add("@InstallmentAmount", dto.InstallmentAmount);
            p.Add("@InstallmentsStartDate", dto.InstallmentsStartDate);
            p.Add("@InstallmentsEndDate", dto.InstallmentsEndDate);
            p.Add("@RemainingAmount", dto.RemainingAmount);
            p.Add("@PaymentMethod", dto.PaymentMethod);
            p.Add("@ProductDeliveryDate", dto.ProductDeliveryDate);
            p.Add("@DeliveryPlace", dto.DeliveryPlace);
            p.Add("@IsProductInspected", dto.IsProductInspected);
            p.Add("@InspectionNotes", dto.InspectionNotes);
            p.Add("@IsReceivedByBuyer", dto.IsReceivedByBuyer);
            p.Add("@TrustReceiptNumber", dto.TrustReceiptNumber);
            p.Add("@TrustReceiptDate", dto.TrustReceiptDate);
            p.Add("@ReceiptAmountNumber", dto.ReceiptAmountNumber);
            p.Add("@ReceiptAmountText", dto.ReceiptAmountText);
            p.Add("@ReceiverName", dto.ReceiverName);
            p.Add("@DelivererName", dto.DelivererName);
            p.Add("@DeliveryReason", dto.DeliveryReason);
            p.Add("@IdentityDocumentNumber", dto.IdentityDocumentNumber);
            p.Add("@Address", dto.Address);
            p.Add("@PhoneNumber", dto.PhoneNumber);
            p.Add("@ReceiverSignature", dto.ReceiverSignature);
            p.Add("@DelivererSignature", dto.DelivererSignature);
            p.Add("@FirstWitnessName", dto.FirstWitnessName);
            p.Add("@FirstWitnessSignature", dto.FirstWitnessSignature);
            p.Add("@SecondWitnessName", dto.SecondWitnessName);
            p.Add("@SecondWitnessSignature", dto.SecondWitnessSignature);
            p.Add("@FirstPartySignature", dto.FirstPartySignature);
            p.Add("@SecondPartySignature", dto.SecondPartySignature);
            p.Add("@CashierSignature", dto.CashierSignature);
            p.Add("@SalesRepresentativeSignature", dto.SalesRepresentativeSignature);
            p.Add("@SalesRepresentativeName", dto.SalesRepresentativeName);
            p.Add("@CashierName", dto.CashierName);
            p.Add("@DelegateID", dto.DelegateID);
            p.Add("@UpdatedByUserID", dto.UpdatedByUserID);
            p.Add("@IsActive", dto.IsActive ?? true);

            await connection.ExecuteAsync("TrustReceipts_Update", p, commandType: CommandType.StoredProcedure);
            return true;
        }

        public async Task<bool> DeleteAsync(int id, int? updatedByUserId)
        {
            using var connection = Connection;
            var parameters = new { TrustReceiptID = id, UpdatedByUserID = updatedByUserId };
            await connection.ExecuteAsync("TrustReceipts_Delete", parameters, commandType: CommandType.StoredProcedure);
            return true;
        }

        public async Task<TrustReceiptDTO?> GetByIdAsync(int id)
        {
            using var connection = Connection;
            var parameters = new { TrustReceiptID = id };
            return await connection.QueryFirstOrDefaultAsync<TrustReceiptDTO>("TrustReceipts_GetByID", parameters, commandType: CommandType.StoredProcedure);
        }

        public async Task<PagedResultDTO<TrustReceiptDTO>> GetPagedAsync(string? searchTerm, int pageNumber, int pageSize, int? delegateId)
        {
            using var connection = Connection;
            var parameters = new DynamicParameters();
            parameters.Add("@SearchTerm", searchTerm);
            parameters.Add("@PageNumber", pageNumber);
            parameters.Add("@PageSize", pageSize);
            parameters.Add("@DelegateID", delegateId);
            parameters.Add("@TotalCount", dbType: DbType.Int32, direction: ParameterDirection.Output);

            var data = await connection.QueryAsync<TrustReceiptDTO>("TrustReceipts_GetPaged", parameters, commandType: CommandType.StoredProcedure);
            int totalCount = parameters.Get<int>("@TotalCount");

            return new PagedResultDTO<TrustReceiptDTO>
            {
                Data = data,
                TotalCount = totalCount
            };
        }
    }
}
