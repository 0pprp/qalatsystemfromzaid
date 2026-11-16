import 'package:customer_app/core/network/api_exception.dart';
import 'package:customer_app/core/push/push_service.dart';
import 'package:customer_app/features/app_config/data/app_config_repository.dart';
import 'package:customer_app/features/orders/data/order_models.dart';
import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('order status mapping', () {
    test('API values map to Arabic labels', () {
      expect(OrderStatus.fromApi('New').label, 'طلب جديد');
      expect(OrderStatus.fromApi('UnderReview').label, 'قيد المعالجة');
      expect(OrderStatus.fromApi('Transferred').label, 'في قسم المبيعات');
      expect(OrderStatus.fromApi('FieldInspection').label, 'قيد الكشف الميداني');
      expect(OrderStatus.fromApi('Sold').label, 'تم البيع - قيد التسديد');
      expect(OrderStatus.fromApi('Finished').label, 'مكتمل التسديد');
      expect(OrderStatus.fromApi('NotSold').label, 'لم يتم البيع');
      expect(OrderStatus.fromApi('Denied').label, 'مرفوض');
      expect(OrderStatus.fromApi('Cancelled').label, 'ملغى');
      expect(OrderStatus.fromApi('unknown'), OrderStatus.newOrder);
    });

    test('open / public reason flags', () {
      expect(OrderStatus.underReview.isOpen, isTrue);
      expect(OrderStatus.sold.isOpen, isFalse);
      expect(OrderStatus.denied.hasPublicReason, isTrue);
      expect(OrderStatus.notSold.hasPublicReason, isTrue);
      expect(OrderStatus.cancelled.hasPublicReason, isFalse);
    });

    test('timeline for an in-progress order shows all steps with progress', () {
      final steps = buildTimelineSteps(OrderStatus.transferred, const []);
      expect(steps.map((s) => s.status), OrderStatus.happyPath);
      expect(steps.where((s) => s.reached).length, 2);
      expect(steps[1].current, isTrue);
    });

    test('timeline for a denied order ends with the denied step', () {
      final steps = buildTimelineSteps(OrderStatus.denied, const [
        TimelineEntry(status: OrderStatus.newOrder, label: 'طلب جديد'),
        TimelineEntry(status: OrderStatus.underReview, label: 'قيد المعالجة'),
        TimelineEntry(status: OrderStatus.denied, label: 'مرفوض'),
      ]);
      expect(steps.map((s) => s.status), [OrderStatus.newOrder, OrderStatus.denied]);
      expect(steps.last.current, isTrue);
    });
  });

  test('push payload to route', () {
    expect(routeForPushData({'type': 'Order', 'orderId': 77}), '/orders/77');
    expect(routeForPushData({'type': 'System', 'productId': '10'}), '/notifications'); // no store pages any more
    expect(routeForPushData({'type': 'System'}), '/notifications');
  });

  test('version comparison for forced update', () {
    expect(compareVersions('1.0.0', '1.0.0'), 0);
    expect(compareVersions('1.0.0', '1.0.1'), lessThan(0));
    expect(compareVersions('1.2.10', '1.2.9'), greaterThan(0));
    expect(compareVersions('1.0.0+5', '1.0'), 0);
  });

  group('error mapping', () {
    final req = RequestOptions(path: 'orders');

    test('server Arabic message and code are kept', () {
      final e = ApiException.from(DioException(
        requestOptions: req,
        type: DioExceptionType.badResponse,
        response: Response(
          requestOptions: req,
          statusCode: 409,
          data: {'error': 'لديك طلب مفتوح', 'code': 'duplicate_open_order', 'orderNumber': 'QO-1'},
        ),
      ));
      expect(e.code, 'duplicate_open_order');
      expect(e.message, 'لديك طلب مفتوح');
      expect(e.stringData('orderNumber'), 'QO-1');
    });

    test('validation fields are parsed', () {
      final e = ApiException.from(DioException(
        requestOptions: req,
        type: DioExceptionType.badResponse,
        response: Response(requestOptions: req, statusCode: 400, data: {
          'error': 'تحقق',
          'code': 'validation',
          'fields': {'firstName': 'مطلوب'},
        }),
      ));
      expect(e.fields['firstName'], 'مطلوب');
    });

    test('network errors become Arabic messages', () {
      final e = ApiException.from(DioException(requestOptions: req, type: DioExceptionType.connectionError));
      expect(e.isNetwork, isTrue);
      expect(e.message, ErrorMessages.network);
      final t = ApiException.from(DioException(requestOptions: req, type: DioExceptionType.receiveTimeout));
      expect(t.code, 'timeout');
    });

    test('missing body falls back to code-based Arabic text', () {
      final e = ApiException.from(DioException(
        requestOptions: req,
        type: DioExceptionType.badResponse,
        response: Response(requestOptions: req, statusCode: 500),
      ));
      expect(e.message, ErrorMessages.server);
    });
  });
}
