import 'package:flutter_test/flutter_test.dart';
import 'package:follower_application/services/follower_media_urls.dart';

void main() {
  test('document urls are rebuilt from apiBase not internal host', () {
    final url = FollowerMediaUrls.resolve(
      apiBase: 'http://169.58.236.52:8081/api/',
      customerId: 53300,
      asyncId: 'ahmed4',
      listId: 1,
      image: {
        'documentId': 9,
        'kind': 'NationalIdFront',
        'url': 'http://127.0.0.1:5402/api/Followers/Customers/53300/documents/9/file?asyncId=x&listId=1',
      },
    );
    expect(url, contains('http://169.58.236.52:8081/api/Followers/Customers/53300/documents/9/file'));
    expect(url, contains('asyncId=ahmed4'));
    expect(url, contains('listId=1'));
    expect(url, isNot(contains('127.0.0.1')));
  });

  test('shop urls use Followers shop-image endpoint', () {
    final url = FollowerMediaUrls.resolve(
      apiBase: 'http://169.58.236.52:8081/api/',
      customerId: 53300,
      asyncId: 'ahmed4',
      listId: 1,
      image: {'kind': 'Shop', 'url': 'http://127.0.0.1:5402/api/x'},
    );
    expect(url, contains('/Followers/Customers/53300/shop-image'));
    expect(url, isNot(contains('127.0.0.1')));
  });
}
