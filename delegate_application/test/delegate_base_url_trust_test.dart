import 'package:delegate_application/services/delegate_base_url_trust.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('allows production HTTP alsaaeidy hosts used by LinkDelegate', () {
    expect(
      DelegateBaseUrlTrust.isTrusted('http://delegatehaidernajaf.alsaaeidy.com/api/'),
      isTrue,
    );
    expect(
      DelegateBaseUrlTrust.isTrusted('http://defaultdata.alsaaeidy.com/GetHaider'),
      isTrue,
    );
  });

  test('allows https and known demo IP', () {
    expect(
      DelegateBaseUrlTrust.isTrusted('https://admincompany.alsaaeidy.com/api/'),
      isTrue,
    );
    expect(
      DelegateBaseUrlTrust.isTrusted('http://169.58.236.52:8081/api/'),
      isTrue,
    );
    expect(DelegateBaseUrlTrust.isTrusted('http://localhost:5000/api/'), isTrue);
  });

  test('rejects arbitrary HTTP hosts (session must not fail-open to phishing)', () {
    expect(
      DelegateBaseUrlTrust.isTrusted('http://evil.example.com/api/'),
      isFalse,
    );
    expect(DelegateBaseUrlTrust.isTrusted('ftp://alsaaeidy.com/api/'), isFalse);
    expect(DelegateBaseUrlTrust.isTrusted(''), isFalse);
  });
}
