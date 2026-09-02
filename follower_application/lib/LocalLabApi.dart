/// Local lab: the follower app must hit this PC, not production Nasiriyah.
class LocalLabApi {
  static const bool enabled = false;

  static List<String> bases() {
    if (!enabled) {
      return [];
    }

    return [
      'http://127.0.0.1:5080/api/',
      'http://10.0.2.2:5080/api/',
      'http://192.168.0.107:5080/api/',
    ];
  }
}
