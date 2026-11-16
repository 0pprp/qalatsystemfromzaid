import 'package:customer_app/core/utils/validators.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('Iraqi phone validator', () {
    test('accepts canonical 07[3-9]XXXXXXXX numbers', () {
      for (final p in ['07701234567', '07801234567', '07501234567', '07901234567', '07301234567']) {
        expect(Validators.isValidIraqiPhone(p), isTrue, reason: p);
        expect(Validators.phone(p), isNull);
      }
    });

    test('rejects wrong prefixes and lengths', () {
      for (final p in ['07201234567', '0770123456', '077012345678', '06701234567', '12345', 'abc']) {
        expect(Validators.isValidIraqiPhone(p), isFalse, reason: p);
      }
      expect(Validators.phone(''), 'رقم الهاتف مطلوب');
      expect(Validators.phone('0720'), isNotNull);
    });

    test('normalises international, spaced and Arabic-Indic input', () {
      expect(Validators.normalizePhone('+964 770 123 4567'), '07701234567');
      expect(Validators.normalizePhone('009647701234567'), '07701234567');
      expect(Validators.normalizePhone('9647701234567'), '07701234567');
      expect(Validators.normalizePhone('7701234567'), '07701234567');
      expect(Validators.normalizePhone('٠٧٧٠١٢٣٤٥٦٧'), '07701234567');
      expect(Validators.isValidIraqiPhone('+9647801234567'), isTrue);
    });

    test('optional phone may be empty', () {
      expect(Validators.phone('', required: false), isNull);
      expect(Validators.phone('123', required: false), isNotNull);
    });
  });

  group('name validators', () {
    test('single Arabic word required', () {
      expect(Validators.nameWord('علي'), isNull);
      expect(Validators.nameWord(''), 'مطلوب');
      expect(Validators.nameWord('علي حسين'), isNotNull);
      expect(Validators.nameWord('Ali'), isNotNull);
      expect(Validators.nameWord('علي1'), isNotNull);
    });

    test('surname optional, may contain spaces', () {
      expect(Validators.surname(''), isNull);
      expect(Validators.surname('آل محمد'), isNull);
      expect(Validators.surname('Smith'), isNotNull);
    });

    test('otp must be 6 digits (Arabic digits allowed)', () {
      expect(Validators.otp('123456'), isNull);
      expect(Validators.otp('١٢٣٤٥٦'), isNull);
      expect(Validators.otp('12345'), isNotNull);
      expect(Validators.otp('12a456'), isNotNull);
    });
  });
}
