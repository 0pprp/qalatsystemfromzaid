namespace BE_Company.Sales.Services
{
    public static class IraqiDinarWords
    {
        private static readonly string[] Ones =
        [
            "", "واحد", "اثنان", "ثلاثة", "أربعة", "خمسة", "ستة", "سبعة", "ثمانية", "تسعة",
            "عشرة", "أحد عشر", "اثنا عشر", "ثلاثة عشر", "أربعة عشر", "خمسة عشر", "ستة عشر",
            "سبعة عشر", "ثمانية عشر", "تسعة عشر"
        ];

        private static readonly string[] Tens =
        [
            "", "", "عشرون", "ثلاثون", "أربعون", "خمسون", "ستون", "سبعون", "ثمانون", "تسعون"
        ];

        public static string ToArabic(decimal amount)
        {
            var value = decimal.Truncate(amount);
            if (value < 0)
            {
                value = 0;
            }

            if (value == 0)
            {
                return "صفر دينار عراقي";
            }

            return $"{Convert((long)value)} دينار عراقي";
        }

        private static string Convert(long n)
        {
            if (n < 20)
            {
                return Ones[n];
            }

            if (n < 100)
            {
                var ten = n / 10;
                var one = n % 10;
                return one == 0 ? Tens[ten] : $"{Ones[one]} و{Tens[ten]}";
            }

            if (n < 1000)
            {
                var h = n / 100;
                var rest = n % 100;
                var hundred = h switch
                {
                    1 => "مائة",
                    2 => "مائتان",
                    _ => $"{Ones[h]}مائة"
                };
                return rest == 0 ? hundred : $"{hundred} و{Convert(rest)}";
            }

            if (n < 1_000_000)
            {
                var thousands = n / 1000;
                var rest = n % 1000;
                var word = thousands switch
                {
                    1 => "ألف",
                    2 => "ألفان",
                    >= 3 and <= 10 => $"{Convert(thousands)} آلاف",
                    _ => $"{Convert(thousands)} ألف"
                };
                return rest == 0 ? word : $"{word} و{Convert(rest)}";
            }

            if (n < 1_000_000_000)
            {
                var millions = n / 1_000_000;
                var rest = n % 1_000_000;
                var word = millions switch
                {
                    1 => "مليون",
                    2 => "مليونان",
                    >= 3 and <= 10 => $"{Convert(millions)} ملايين",
                    _ => $"{Convert(millions)} مليون"
                };
                return rest == 0 ? word : $"{word} و{Convert(rest)}";
            }

            var billions = n / 1_000_000_000;
            var remainder = n % 1_000_000_000;
            var billionWord = billions == 1 ? "مليار" : $"{Convert(billions)} مليار";
            return remainder == 0 ? billionWord : $"{billionWord} و{Convert(remainder)}";
        }
    }
}
