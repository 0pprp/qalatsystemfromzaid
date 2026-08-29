CREATE TABLE [dbo].[TrustReceipts] (
    [TrustReceiptID] INT IDENTITY(1,1) NOT NULL,
    
    -- General Contract Info
    [ContractNumber] NVARCHAR(100) NULL,
    [ContractDate] DATETIME NULL,
    [ContractType] NVARCHAR(100) NULL,
    [ContractNotes] NVARCHAR(MAX) NULL,
    [ContractStatus] NVARCHAR(100) NULL,

    -- First Party Info
    [FirstPartyName] NVARCHAR(255) NULL,
    [CompanyType] NVARCHAR(100) NULL,
    [CompanyRepresentativeName] NVARCHAR(255) NULL,
    [CompanyRepresentativeRole] NVARCHAR(100) NULL,

    -- Second Party / Buyer Info
    [BuyerID] INT NULL,
    [BuyerName] NVARCHAR(255) NULL,
    [BuyerNationalCardNumber] NVARCHAR(100) NULL,
    [BuyerGovernorate] NVARCHAR(100) NULL,
    [BuyerWorkOrResidenceAddress] NVARCHAR(255) NULL,
    [BuyerNearestLandmark] NVARCHAR(255) NULL,
    [BuyerPhoneNumber] NVARCHAR(50) NULL,
    [BuyerRationCenterNumber] NVARCHAR(100) NULL,
    [BuyerAffiliation] NVARCHAR(150) NULL,
    [BuyerMukhtarName] NVARCHAR(255) NULL,

    -- Product Info
    [ProductID] INT NULL,
    [ProductType] NVARCHAR(150) NULL,
    [ProductName] NVARCHAR(255) NULL,
    [ProductDescription] NVARCHAR(MAX) NULL,
    [ProductNumber] NVARCHAR(100) NULL,
    [ProductDeliveryCondition] NVARCHAR(150) NULL,

    -- Financial Info
    [TotalAmountNumber] FLOAT NULL,
    [TotalAmountText] NVARCHAR(255) NULL,
    [FirstInstallmentAmount] FLOAT NULL,
    [FirstInstallmentDate] DATETIME NULL,
    [InstallmentsCount] INT NULL,
    [InstallmentAmount] FLOAT NULL,
    [InstallmentsStartDate] DATETIME NULL,
    [InstallmentsEndDate] DATETIME NULL,
    [RemainingAmount] FLOAT NULL,
    [PaymentMethod] NVARCHAR(100) NULL,

    -- Delivery Info
    [ProductDeliveryDate] DATETIME NULL,
    [DeliveryPlace] NVARCHAR(255) NULL,
    [IsProductInspected] BIT NULL DEFAULT (0),
    [InspectionNotes] NVARCHAR(MAX) NULL,
    [IsReceivedByBuyer] BIT NULL DEFAULT (0),

    -- Trust Receipt Info
    [TrustReceiptNumber] NVARCHAR(100) NULL,
    [TrustReceiptDate] DATETIME NULL,
    [ReceiptAmountNumber] FLOAT NULL,
    [ReceiptAmountText] NVARCHAR(255) NULL,
    [ReceiverName] NVARCHAR(255) NULL,
    [DelivererName] NVARCHAR(255) NULL,
    [DeliveryReason] NVARCHAR(255) NULL,
    [IdentityDocumentNumber] NVARCHAR(100) NULL,
    [Address] NVARCHAR(255) NULL,
    [PhoneNumber] NVARCHAR(50) NULL,
    
    -- Witnesses & Signatures (Names)
    [ReceiverSignature] NVARCHAR(255) NULL,
    [DelivererSignature] NVARCHAR(255) NULL,
    [FirstWitnessName] NVARCHAR(255) NULL,
    [FirstWitnessSignature] NVARCHAR(255) NULL,
    [SecondWitnessName] NVARCHAR(255) NULL,
    [SecondWitnessSignature] NVARCHAR(255) NULL,
    [FirstPartySignature] NVARCHAR(255) NULL,
    [SecondPartySignature] NVARCHAR(255) NULL,
    [CashierSignature] NVARCHAR(255) NULL,
    [SalesRepresentativeSignature] NVARCHAR(255) NULL,
    [SalesRepresentativeName] NVARCHAR(255) NULL,
    [CashierName] NVARCHAR(255) NULL,

    -- Delegate Info
    [DelegateID] INT NULL,

    -- System Fields
    [CreatedByUserID] INT NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedByUserID] INT NULL,
    [UpdatedDate] DATETIME NULL,
    [IsDelete] BIT NOT NULL DEFAULT (0),
    [IsActive] BIT NOT NULL DEFAULT (1),
    
    PRIMARY KEY CLUSTERED ([TrustReceiptID] ASC)
);
