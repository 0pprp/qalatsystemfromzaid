using BE_Company.Sales.Authorization;
using BE_Company.Sales.DTO;
using BE_Company.Sales.Models;
using BE_Company.Sales.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BE_Company.Sales.Tests
{
    public class SalesPostingTests
    {
        private static readonly DateTime MorningIraqUtc =
            IraqTimeService.ToUtcFromIraq(new DateTime(2026, 9, 7, 10, 0, 0));
        private static readonly DateTime JustBeforePostingUtc =
            IraqTimeService.ToUtcFromIraq(new DateTime(2026, 9, 7, 15, 59, 0));
        private static readonly DateTime PostingStartUtc =
            IraqTimeService.ToUtcFromIraq(new DateTime(2026, 9, 7, 16, 0, 0));
        private static readonly DateTime EveningIraqUtc =
            IraqTimeService.ToUtcFromIraq(new DateTime(2026, 9, 7, 17, 0, 0));

        private static SalesIdentity Identity() => new()
        {
            EmployeeId = 1,
            EmployeeName = "موظف",
            BranchId = "najaf-demo",
            BranchName = "النجف",
            Role = SalesRoles.SalesEmployee,
            UserType = SalesRoles.UserTypeSalesEmployee
        };

        [Theory]
        [MemberData(nameof(AnyTimeOfDay))]
        public async Task Complete_AtAnyTime_PostsMainSystemImmediately(DateTime utcNow)
        {
            var repo = Seed();
            repo.Sales[10].CustomerId = 42;
            var clock = new FakeClock { UtcNow = utcNow };
            var posting = CreatePosting(repo);
            var svc = CreateComplete(repo, posting, clock);

            var result = await svc.CompleteAsync(10, Identity(), CancellationToken.None);

            Assert.Equal(SalesStatuses.Completed, result.Status);
            Assert.Equal(SalesPostingStatuses.Posted, repo.Sales[10].PostingStatus);
            Assert.NotNull(repo.Sales[10].PostedAtUtc);
            Assert.Equal(1, repo.MainPostingCount);
            Assert.Equal(2, repo.MainStock[5]);
            Assert.Single(repo.Payments);
            Assert.Equal(0, await posting.PostDueSalesAsync(CancellationToken.None));
        }

        public static IEnumerable<object[]> AnyTimeOfDay() =>
        [
            [MorningIraqUtc],
            [JustBeforePostingUtc],
            [PostingStartUtc],
            [EveningIraqUtc],
            [IraqTimeService.ToUtcFromIraq(new DateTime(2026, 9, 7, 1, 30, 0))]
        ];

        [Fact]
        public async Task Complete_DeductsEmployeeStockImmediately()
        {
            var repo = Seed();
            repo.Stock[5] = 3;
            var clock = new FakeClock { UtcNow = MorningIraqUtc };
            var svc = CreateComplete(repo, CreatePosting(repo), clock);

            await svc.CompleteAsync(10, Identity(), CancellationToken.None);

            Assert.Equal(2, repo.Stock[5]);
            Assert.Equal(1, repo.DeductionCount);
            Assert.Equal(SalesStatuses.Completed, repo.Sales[10].Status);
        }

        [Fact]
        public async Task Morning_PostsSaleOnce_AndRecordsDownPayment()
        {
            var repo = Seed();
            repo.Sales[10].CustomerId = 42;
            repo.Sales[10].OverrideDownPayment = 200000;
            repo.Sales[10].DownPayment = 200000;
            var clock = new FakeClock { UtcNow = MorningIraqUtc };
            var posting = CreatePosting(repo);
            var svc = CreateComplete(repo, posting, clock);

            await svc.CompleteAsync(10, Identity(), CancellationToken.None);
            var again = await posting.PostDueSalesAsync(CancellationToken.None);

            Assert.Equal(0, again);
            Assert.Equal(SalesStatuses.Completed, repo.Sales[10].Status);
            Assert.Equal(SalesPostingStatuses.Posted, repo.Sales[10].PostingStatus);
            Assert.NotNull(repo.Sales[10].PostedAtUtc);
            Assert.Equal(1, repo.MainPostingCount);
            Assert.Equal(2, repo.MainStock[5]);
            var payment = Assert.Single(repo.Payments);
            Assert.Equal(10, payment.SaleId);
            Assert.Equal(42, payment.CustomerId);
            Assert.Equal(200000, payment.AmountDenar);
            Assert.Equal(payment.CustomerPaymentId, repo.Sales[10].DownPaymentCustomerPaymentId);
            Assert.Equal(800000, SalesDownPaymentReceipt.Remaining(repo.Sales[10].FinalSalePrice, repo.ReceiptsTotal));
        }

        [Fact]
        public async Task Retry_DoesNotRepeatSaleStockOrReceipt()
        {
            var repo = Seed();
            repo.Sales[10].CustomerId = 42;
            repo.Sales[10].DownPayment = 200000;
            var clock = new FakeClock { UtcNow = EveningIraqUtc };
            var posting = CreatePosting(repo);
            repo.FailMainPosting = true;

            await CreateComplete(repo, posting, clock)
                .CompleteAsync(10, Identity(), CancellationToken.None);

            Assert.Equal(SalesStatuses.Completed, repo.Sales[10].Status);
            Assert.Equal(SalesPostingStatuses.Failed, repo.Sales[10].PostingStatus);
            Assert.Empty(repo.Payments);
            Assert.Equal(3, repo.MainStock[5]);
            Assert.Equal(2, repo.Stock[5]);
            Assert.True(repo.Sales[10].PostingAttempts >= 1);

            repo.FailMainPosting = false;
            await posting.PostSaleAsync(10, CancellationToken.None);
            await posting.PostSaleAsync(10, CancellationToken.None);
            await posting.PostDueSalesAsync(CancellationToken.None);

            Assert.Equal(SalesStatuses.Completed, repo.Sales[10].Status);
            Assert.Equal(SalesPostingStatuses.Posted, repo.Sales[10].PostingStatus);
            Assert.Equal(1, repo.MainPostingCount);
            Assert.Equal(2, repo.MainStock[5]);
            Assert.Single(repo.Payments);
            Assert.Equal(2, repo.Stock[5]);
        }

        [Fact]
        public async Task Worker_PicksUpLeftoverPendingSale_AtAnyTime()
        {
            var repo = Seed();
            repo.Sales[10].CustomerId = 42;
            repo.Sales[10].OverrideDownPayment = 150000;
            repo.Sales[10].DownPayment = 150000;
            var morning = new FakeClock { UtcNow = MorningIraqUtc };
            // No posting service: simulates a sale completed before this change
            // (or a crash before the immediate post) that is still Pending.
            await new SalesCompleteService(repo, new FakeDraftRepository(), new FakeDocumentService(), clock: morning)
                .CompleteAsync(10, Identity(), CancellationToken.None);

            Assert.Equal(SalesPostingStatuses.Pending, repo.Sales[10].PostingStatus);
            Assert.Empty(repo.Payments);
            Assert.Equal(3, repo.MainStock[5]);

            var restarted = CreatePosting(repo);
            var posted = await restarted.PostDueSalesAsync(CancellationToken.None);

            Assert.Equal(1, posted);
            Assert.Equal(SalesPostingStatuses.Posted, repo.Sales[10].PostingStatus);
            Assert.Equal(1, repo.MainPostingCount);
            Assert.Equal(2, repo.MainStock[5]);
            Assert.Single(repo.Payments);
            Assert.Equal(150000, repo.ReceiptsTotal);
        }

        [Fact]
        public async Task Posting_UpdatesOfficialCustomerName_WithoutChangingCompleted()
        {
            var repo = Seed();
            repo.Sales[10].CustomerId = 42;
            repo.Sales[10].FullName = "كرار كاظم";
            repo.Sales[10].SalesRequestId = 8;
            repo.RequestNames[8] = "كرار كاظم حسن";
            repo.OfficialCustomers[42] = "كرار كاظم";
            var clock = new FakeClock { UtcNow = EveningIraqUtc };
            var posting = CreatePosting(repo);

            await CreateComplete(repo, posting, clock)
                .CompleteAsync(10, Identity(), CancellationToken.None);

            Assert.Equal(SalesStatuses.Completed, repo.Sales[10].Status);
            Assert.Equal("كرار كاظم حسن", repo.Sales[10].FullName);
            Assert.Equal("كرار كاظم حسن", repo.OfficialCustomers[42]);
            Assert.Equal(SalesPostingStatuses.Posted, repo.Sales[10].PostingStatus);
        }

        [Fact]
        public async Task AlreadyPostedOldSale_IsNotPickedByWorker()
        {
            var repo = Seed(SalesStatuses.Completed);
            repo.Sales[10].PostingStatus = SalesPostingStatuses.Posted;
            repo.Sales[10].CustomerId = 42;
            repo.Sales[10].DownPayment = 200000;
            var clock = new FakeClock { UtcNow = EveningIraqUtc };
            var posting = CreatePosting(repo);

            Assert.Equal(0, await posting.PostDueSalesAsync(CancellationToken.None));
            Assert.Empty(repo.Payments);
            Assert.Equal(0, repo.MainPostingCount);
            Assert.Equal(3, repo.MainStock[5]);
        }

        private static SalesPostingService CreatePosting(FakeCompleteRepository repo) =>
            new(repo, NullLogger<SalesPostingService>.Instance);

        private static SalesCompleteService CreateComplete(
            FakeCompleteRepository repo,
            ISalesPostingService posting,
            IIraqClock clock) =>
            new(repo, new FakeDraftRepository(), new FakeDocumentService(), posting: posting, clock: clock);

        private static FakeCompleteRepository Seed(string status = "Pending")
        {
            var repo = new FakeCompleteRepository();
            repo.Sales[10] = new SalesDraftDTO
            {
                SaleId = 10,
                EmployeeId = 1,
                CityValue = "najaf-demo",
                Status = status,
                FullName = "أحمد علي",
                Phone = "07701234567",
                Province = "النجف",
                NationalCardNumber = "N1",
                Address = "حي الأنصار",
                NearestLandmark = "جامع",
                MukhtarName = "حسن",
                RationCenterNumber = "12",
                EvaluationLevel = SalesEvaluationLevels.Good,
                EvaluationNote = "ملاحظة",
                BaseSalePrice = 1000000,
                FinalSalePrice = 1000000,
                DailyInstallment = 25000,
                OverrideDownPayment = 200000,
                DownPayment = 200000,
                PostingStatus = status == SalesStatuses.Completed
                    ? SalesPostingStatuses.Posted
                    : SalesPostingStatuses.Pending,
                Items =
                [
                    new SalesDraftItemDTO
                    {
                        ProductId = 5,
                        ProductName = "ثلاجة سامسونج",
                        Quantity = 1,
                        UnitSalePrice = 1000000,
                        LineSalePrice = 1000000
                    }
                ]
            };
            repo.Stock[5] = 3;
            repo.MainStock[5] = 3;
            return repo;
        }
    }
}
