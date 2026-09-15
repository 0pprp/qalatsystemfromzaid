import 'package:delegated_manager_application/core/auth/session.dart';
import 'package:delegated_manager_application/core/config/app_env.dart';
import 'package:delegated_manager_application/core/network/api_client.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  setUp(() {
    Session.token = null;
    Session.apiBase = null;
  });

  test('paths are appended to the resolved gateway base', () {
    final uri = ApiClient.uri('delegated-manager/complaints', {'page': '1'});

    expect(uri.toString(),
        '${AppEnv.productionHostFallback}delegated-manager/complaints?page=1');
  });

  test('a leading api/ segment is not duplicated', () {
    expect(
      ApiClient.uri('api/delegated-manager/exceptions').toString(),
      '${AppEnv.productionHostFallback}delegated-manager/exceptions',
    );
    expect(
      ApiClient.uri('/delegated-manager/dashboard').toString(),
      '${AppEnv.productionHostFallback}delegated-manager/dashboard',
    );
  });

  test('empty query values are dropped', () {
    final uri = ApiClient.uri('delegated-manager/exceptions', {
      'status': 'Pending',
      'cityValue': '',
    });

    expect(uri.queryParameters, {'status': 'Pending'});
  });

  test('the bearer token is attached only when a session exists', () {
    expect(ApiClient.headers().containsKey('Authorization'), isFalse);

    Session.token = 'jwt-token';
    expect(ApiClient.headers()['Authorization'], 'Bearer jwt-token');
  });
}
