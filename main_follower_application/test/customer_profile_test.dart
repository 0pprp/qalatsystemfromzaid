import 'package:flutter_test/flutter_test.dart';
import 'package:main_follower_application/core/config/province_labels.dart';
import 'package:main_follower_application/domain/customer_profile.dart';

void main() {
  group('ProvinceLabels', () {
    test('demo najaf is friendly', () {
      expect(
        ProvinceLabels.display('DatabaseCompanyNajaf_DEMO'),
        'النجف - DEMO',
      );
    });

    test('basra friendly', () {
      expect(ProvinceLabels.display('DatabaseCompanyBasra'), 'البصرة');
    });
  });

  group('CustomerProfile parsing', () {
    test('maps documents notes evaluations sales', () {
      final p = CustomerProfile.fromJson({
        'customerId': 9,
        'customerName': 'زبون',
        'customerDocuments': [
          {'id': 1, 'documentType': 'NationalIdFront'},
        ],
        'notes': [
          {'note': 'ملاحظة', 'userType': 'متابع فرعي', 'userName': 'ahmed'},
        ],
        'evaluations': [
          {
            'saleId': 5,
            'evaluationLevel': 4,
            'evaluationName': 'جيد جداً',
            'evaluationNote': 'من المتابع',
          },
        ],
        'sales': [
          {'saleId': 5, 'status': 'Completed', 'employeeName': 'e1'},
        ],
      });
      expect(p.documents.single.displayLabel, contains('وطنية'));
      expect(p.notes.single.authorRole, 'متابع فرعي');
      expect(p.evaluations.single.name, 'جيد جداً');
      expect(p.sales.single.statusLabel, contains('مصادق'));
    });
  });
}
