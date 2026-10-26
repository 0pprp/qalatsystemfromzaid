import 'package:flutter/widgets.dart';
import 'package:webview_flutter_platform_interface/webview_flutter_platform_interface.dart';

/// Minimal WebView platform for widget tests (no native platform channels).
class FakeWebViewPlatform extends WebViewPlatform {
  FakeWebViewPlatform({this.failLoad = false});

  final bool failLoad;

  @override
  PlatformWebViewController createPlatformWebViewController(
    PlatformWebViewControllerCreationParams params,
  ) =>
      FakeWebViewController(params, failLoad: failLoad);

  @override
  PlatformNavigationDelegate createPlatformNavigationDelegate(
    PlatformNavigationDelegateCreationParams params,
  ) =>
      FakeNavigationDelegate(params);

  @override
  PlatformWebViewWidget createPlatformWebViewWidget(
    PlatformWebViewWidgetCreationParams params,
  ) =>
      FakeWebViewWidget(params);

  @override
  PlatformWebViewCookieManager createPlatformCookieManager(
    PlatformWebViewCookieManagerCreationParams params,
  ) =>
      FakeCookieManager(params);
}

class FakeNavigationDelegate extends PlatformNavigationDelegate {
  FakeNavigationDelegate(super.params) : super.implementation();

  PageEventCallback? onPageFinished;
  WebResourceErrorCallback? onWebResourceError;

  @override
  Future<void> setOnPageFinished(PageEventCallback callback) async {
    onPageFinished = callback;
  }

  @override
  Future<void> setOnWebResourceError(
    WebResourceErrorCallback callback,
  ) async {
    onWebResourceError = callback;
  }
}

class FakeWebViewController extends PlatformWebViewController {
  FakeWebViewController(super.params, {this.failLoad = false})
      : super.implementation();

  final bool failLoad;
  FakeNavigationDelegate? _delegate;

  @override
  Future<void> setPlatformNavigationDelegate(
    PlatformNavigationDelegate handler,
  ) async {
    _delegate = handler as FakeNavigationDelegate;
  }

  @override
  Future<void> loadRequest(LoadRequestParams params) async {
    final url = params.uri.toString();
    // Defer so the first frame can paint the splash overlay in widget tests.
    await Future<void>.delayed(Duration.zero);
    if (failLoad) {
      _delegate?.onWebResourceError?.call(
        const WebResourceError(
          errorCode: 1,
          description: 'تعذر تحميل الصفحة',
          errorType: WebResourceErrorType.hostLookup,
        ),
      );
      return;
    }
    _delegate?.onPageFinished?.call(url);
  }

  @override
  Future<void> setJavaScriptMode(JavaScriptMode javaScriptMode) async {}

  @override
  Future<void> setBackgroundColor(Color color) async {}

  @override
  Future<void> runJavaScript(String javaScript) async {}
}

class FakeWebViewWidget extends PlatformWebViewWidget {
  FakeWebViewWidget(super.params) : super.implementation();

  @override
  Widget build(BuildContext context) {
    return const SizedBox(key: Key('fake-webview'));
  }
}

class FakeCookieManager extends PlatformWebViewCookieManager {
  FakeCookieManager(super.params) : super.implementation();
}
