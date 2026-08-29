class Formatters {
  static String formatNumber(double number) {
    String numberStr =
        number % 1 == 0 ? number.toInt().toString() : number.toString();
    String reversedStr = numberStr.split('').reversed.join('');
    String formattedReversedStr =
        reversedStr.replaceAllMapped(RegExp(r'\d{3}'), (match) {
      return '${match.group(0)},';
    });
    String formattedStr = formattedReversedStr
        .split('')
        .reversed
        .join('')
        .replaceFirst(RegExp(r'^,'), '');
    return formattedStr;
  }

  static String formatDate(String dateString) {
    if (dateString.isEmpty) return 'لا يوجد';
    try {
      DateTime parsedDate = DateTime.parse(dateString);
      return '${parsedDate.year}/${parsedDate.month.toString().padLeft(2, '0')}/${parsedDate.day.toString().padLeft(2, '0')}';
    } catch (e) {
      return dateString;
    }
  }
}
