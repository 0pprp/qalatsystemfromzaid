/// جزء نص داخل فقرة PDF. منسوخ من أداة العقد المرجعية.
class PdfTextPart {
  const PdfTextPart(this.text, {this.isUserField = false});

  final String text;
  final bool isUserField;
}

/// فقرة PDF مكوّنة من أجزاء ثابتة وحقول مستخدم.
class PdfRichParagraph {
  const PdfRichParagraph(this.parts);

  final List<PdfTextPart> parts;

  String get plainText => parts.map((p) => p.text).join();
}

/// بناء فقرات PDF مع تمييز حقول المستخدم.
class PdfParagraphBuilder {
  final List<PdfTextPart> _parts = [];

  void text(String value) => _parts.add(PdfTextPart(value));

  /// نص ثابت بين أقواس. بدون علامات اتجاه غير موجودة في خط Cairo.
  void staticParens(String inner) {
    _parts.add(PdfTextPart('($inner)'));
  }

  /// قيمة مستخدم بين أقواس. بدون LRM حتى لا تظهر � حول القيمة.
  void fieldInParens(String value) {
    final trimmed = value.trim();
    if (trimmed.isEmpty) {
      _parts.add(const PdfTextPart('( .............................. )'));
      return;
    }
    _parts.add(PdfTextPart('( $trimmed )', isUserField: true));
  }

  PdfRichParagraph build() => PdfRichParagraph(List.unmodifiable(_parts));
}
