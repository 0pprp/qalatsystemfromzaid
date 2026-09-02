namespace BE_Company.Sales.Authorization
{
    public static class SalesEvaluationLevels
    {
        public const int Rejected = 1;
        public const int Accepted = 2;
        public const int Good = 3;
        public const int VeryGood = 4;
        public const int Excellent = 5;

        public static bool IsKnown(int level) =>
            level is Rejected or Accepted or Good or VeryGood or Excellent;

        public static string DisplayName(int level) => level switch
        {
            Rejected => "مرفوض",
            Accepted => "مقبول",
            Good => "جيد",
            VeryGood => "جيد جداً",
            Excellent => "ممتاز",
            _ => level.ToString()
        };
    }

    public static class SalesStatuses
    {
        public const string Pending = "Pending";
        public const string Rejected = "Rejected";
        public const string Completed = "Completed";
        public const string DocumentsPending = "DocumentsPending";
        public const string DocumentsReady = "DocumentsReady";
    }
}
