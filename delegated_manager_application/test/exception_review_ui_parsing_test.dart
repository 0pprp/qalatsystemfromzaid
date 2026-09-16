import 'package:delegated_manager_application/features/complaints/domain/complaint.dart';
import 'package:delegated_manager_application/features/exceptions/domain/sales_exception.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('complaint friendlySource never exposes raw app key when label present',
      () {
    final c = ComplaintSummary.fromJson({
      'id': '11111111-1111-1111-1111-111111111111',
      'subject': 'شكوى',
      'status': 'Unread',
      'senderDisplayName': 'أحمد',
      'sourceApp': 'delegate_application',
      'sourceLabel': 'مندوب',
    });
    expect(c.friendlySource, 'مندوب');
    expect(c.friendlySource.contains('delegate'), isFalse);
  });

  test('two-part name classification stays New in review payload', () {
    final detail = ExceptionDetail.fromJson({
      'request': {
        'id': '33333333-3333-3333-3333-333333333333',
        'status': 'Pending',
        'customerName': 'علي حسن',
      },
      'audit': [],
      'review': {
        'customerClassification': {
          'type': 'New',
          'labelArabic': 'زبون جديد',
          'matchCount': 0,
          'explanationArabic':
              'لم يتم العثور على تطابق في نفس المحافظة برقم الهاتف أو الاسم الثلاثي',
        },
        'source': {
          'displayLabel': 'غير متوفر',
          'personName': 'غير متوفر',
          'branchName': 'غير متوفر',
        },
        'matchingCustomers': [],
      },
    });
    expect(detail.review!.customerClassification.isNew, isTrue);
    expect(detail.review!.matchingCustomers, isEmpty);
  });

  test('previous sale metrics parse independently', () {
    final detail = ExceptionDetail.fromJson({
      'request': {
        'id': '44444444-4444-4444-4444-444444444444',
        'status': 'Pending',
        'customerName': 'محمد علي حسن',
      },
      'audit': [],
      'review': {
        'customerClassification': {
          'type': 'Existing',
          'labelArabic': 'زبون موجود',
          'matchCount': 1,
          'explanationArabic': 'تطابق بالهاتف',
        },
        'source': {
          'displayLabel': 'مندوب',
          'personName': 'أحمد',
          'branchName': 'البصرة',
        },
        'matchingCustomers': [
          {
            'fullName': 'محمد علي حسن',
            'matchReasons': ['هاتف'],
            'ratingLabel': 'جيد',
            'financialSummary': {
              'totalSales': 1250000,
              'totalPaid': 1000000,
              'remaining': 250000,
            },
            'previousSales': [
              {
                'saleDate': '2026-02-15T00:00:00Z',
                'productOrType': 'ثلاجة',
                'saleAmount': 1250000,
                'paidAmount': 1000000,
                'remainingAmount': 250000,
                'accountZero': false,
                'accountStatusArabic': 'مفتوح',
                'paymentCount': 8,
                'repaymentDays': 142,
                'lastPaymentDate': '2026-07-10T00:00:00Z',
                'friendlyCityName': 'البصرة',
              },
              {
                'saleDate': '2025-01-01T00:00:00Z',
                'saleAmount': 500000,
                'paidAmount': 500000,
                'remainingAmount': 0,
                'accountZero': true,
                'accountStatusArabic': 'مصفر',
                'paymentCount': 3,
                'repaymentDays': 90,
              },
            ],
          },
        ],
      },
    });

    final sales = detail.review!.matchingCustomers.single.previousSales;
    expect(sales.length, 2);
    expect(sales[0].paymentCount, 8);
    expect(sales[0].repaymentDays, 142);
    expect(sales[1].paymentCount, 3);
    expect(sales[1].repaymentDays, 90);
    expect(sales[0].paymentCount, isNot(sales[1].paymentCount));
  });
}
