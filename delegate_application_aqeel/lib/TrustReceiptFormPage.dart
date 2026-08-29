import 'dart:convert';
import 'package:delegate_application/utils/AppTheme.dart';
import 'package:flutter/material.dart';
import 'package:http/http.dart' as http;
import 'package:shared_preferences/shared_preferences.dart';
import 'package:intl/intl.dart' hide TextDirection;

class TrustReceiptFormPage extends StatefulWidget {
  const TrustReceiptFormPage({super.key});

  @override
  _TrustReceiptFormPageState createState() => _TrustReceiptFormPageState();
}

class _TrustReceiptFormPageState extends State<TrustReceiptFormPage> {
  final _formKey = GlobalKey<FormState>();
  bool _isLoading = false;
  int? _receiptId;

  // Controllers for all text fields
  final Map<String, TextEditingController> _ctrls = {};

  final List<String> _textFields = [
    'contractNumber', 'contractType', 'contractNotes', 'contractStatus',
    'firstPartyName', 'companyType', 'companyRepresentativeName', 'companyRepresentativeRole',
    'buyerName', 'buyerNationalCardNumber', 'buyerGovernorate', 'buyerWorkOrResidenceAddress',
    'buyerNearestLandmark', 'buyerPhoneNumber', 'buyerRationCenterNumber', 'buyerAffiliation', 'buyerMukhtarName',
    'productType', 'productName', 'productDescription', 'productNumber', 'productDeliveryCondition',
    'totalAmountNumber', 'totalAmountText', 'firstInstallmentAmount', 'installmentsCount', 'installmentAmount',
    'remainingAmount', 'paymentMethod',
    'deliveryPlace', 'inspectionNotes',
    'trustReceiptNumber', 'receiptAmountNumber', 'receiptAmountText', 'receiverName', 'delivererName',
    'deliveryReason', 'identityDocumentNumber', 'address', 'phoneNumber',
    'firstWitnessName', 'secondWitnessName',
    'salesRepresentativeName', 'cashierName'
  ];

  // Date fields
  DateTime? _contractDate;
  DateTime? _firstInstallmentDate;
  DateTime? _installmentsStartDate;
  DateTime? _installmentsEndDate;
  DateTime? _productDeliveryDate;
  DateTime? _trustReceiptDate;

  // Bool fields
  bool _isProductInspected = false;
  bool _isReceivedByBuyer = false;

  @override
  void initState() {
    super.initState();
    for (var field in _textFields) {
      _ctrls[field] = TextEditingController();
    }
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    final args = ModalRoute.of(context)?.settings.arguments;
    if (args != null && args is Map<String, dynamic>) {
      _receiptId = args['trustReceiptID'];
      _populateData(args);
    }
  }

  void _populateData(Map<String, dynamic> data) {
    for (var field in _textFields) {
      if (data[field] != null) {
        _ctrls[field]!.text = data[field].toString();
      }
    }
    _contractDate = _parseDate(data['contractDate']);
    _firstInstallmentDate = _parseDate(data['firstInstallmentDate']);
    _installmentsStartDate = _parseDate(data['installmentsStartDate']);
    _installmentsEndDate = _parseDate(data['installmentsEndDate']);
    _productDeliveryDate = _parseDate(data['productDeliveryDate']);
    _trustReceiptDate = _parseDate(data['trustReceiptDate']);
    _isProductInspected = data['isProductInspected'] == true;
    _isReceivedByBuyer = data['isReceivedByBuyer'] == true;
  }

  DateTime? _parseDate(dynamic val) {
    if (val == null) return null;
    try {
      return DateTime.parse(val.toString());
    } catch (_) {
      return null;
    }
  }

  @override
  void dispose() {
    for (var c in _ctrls.values) {
      c.dispose();
    }
    super.dispose();
  }

  Future<void> _pickDate(String label, DateTime? current, ValueChanged<DateTime> onPicked) async {
    final picked = await showDatePicker(
      context: context,
      initialDate: current ?? DateTime.now(),
      firstDate: DateTime(2000),
      lastDate: DateTime(2100),
      builder: (context, child) => Theme(
        data: Theme.of(context).copyWith(
          colorScheme: const ColorScheme.light(primary: AppTheme.primaryColor),
        ),
        child: child!,
      ),
    );
    if (picked != null) {
      setState(() => onPicked(picked));
    }
  }

  String _fmtDate(DateTime? d) {
    if (d == null) return '';
    return DateFormat('yyyy/MM/dd').format(d);
  }

  Future<void> _save() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() => _isLoading = true);

    try {
      final prefs = await SharedPreferences.getInstance();
      final linkDelegate = prefs.getString('LinkDelegate') ?? '0';
      final delegateId = int.parse(prefs.getString('DelegateID') ?? '0');

      final Map<String, dynamic> payload = {};

      // Text fields
      for (var field in _textFields) {
        var text = _ctrls[field]!.text;
        if (text.isNotEmpty) {
          if (field == 'totalAmountNumber' || field == 'firstInstallmentAmount' ||
              field == 'installmentAmount' || field == 'remainingAmount' ||
              field == 'receiptAmountNumber') {
            payload[field] = double.tryParse(text);
          } else if (field == 'installmentsCount') {
            payload[field] = int.tryParse(text);
          } else {
            payload[field] = text;
          }
        }
      }

      // Date fields
      payload['contractDate'] = _contractDate?.toIso8601String();
      payload['firstInstallmentDate'] = _firstInstallmentDate?.toIso8601String();
      payload['installmentsStartDate'] = _installmentsStartDate?.toIso8601String();
      payload['installmentsEndDate'] = _installmentsEndDate?.toIso8601String();
      payload['productDeliveryDate'] = _productDeliveryDate?.toIso8601String();
      payload['trustReceiptDate'] = _trustReceiptDate?.toIso8601String();

      // Bool fields
      payload['isProductInspected'] = _isProductInspected;
      payload['isReceivedByBuyer'] = _isReceivedByBuyer;

      // Signature fields - always empty (filled in physically on printed document)
      payload['receiverSignature'] = '';
      payload['delivererSignature'] = '';
      payload['firstWitnessSignature'] = '';
      payload['secondWitnessSignature'] = '';
      payload['firstPartySignature'] = '';
      payload['secondPartySignature'] = '';
      payload['cashierSignature'] = '';
      payload['salesRepresentativeSignature'] = '';

      // System
      payload['createdByUserID'] = delegateId;
      payload['delegateID'] = delegateId;
      if (_receiptId != null) {
        payload['updatedByUserID'] = delegateId;
        payload['trustReceiptID'] = _receiptId;
      }

      final uri = _receiptId == null
          ? Uri.parse('${linkDelegate}TrustReceipts')
          : Uri.parse('${linkDelegate}TrustReceipts/$_receiptId');
      final method = _receiptId == null ? http.post : http.put;

      final response = await method(
        uri,
        headers: {"Content-Type": "application/json"},
        body: json.encode(payload),
      );

      if (!mounted) return;
      if (response.statusCode == 200 || response.statusCode == 201 || response.statusCode == 204) {
        Navigator.pop(context, true);
      } else {
        _showError('حدث خطأ: ${response.statusCode}');
      }
    } catch (e) {
      if (mounted) _showError('حدث خطأ: $e');
    } finally {
      if (mounted) setState(() => _isLoading = false);
    }
  }

  void _showError(String msg) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(msg, style: const TextStyle(fontFamily: 'Cairo'))),
    );
  }

  Widget _buildTextField(String key, String label, {TextInputType type = TextInputType.text}) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 8.0),
      child: TextFormField(
        controller: _ctrls[key],
        keyboardType: type,
        decoration: InputDecoration(
          labelText: label,
          labelStyle: const TextStyle(fontFamily: 'Cairo'),
          border: OutlineInputBorder(borderRadius: BorderRadius.circular(10)),
        ),
      ),
    );
  }

  Widget _buildDateField(String label, DateTime? value, ValueChanged<DateTime> onPicked) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 8.0),
      child: InkWell(
        onTap: () => _pickDate(label, value, onPicked),
        child: InputDecorator(
          decoration: InputDecoration(
            labelText: label,
            labelStyle: const TextStyle(fontFamily: 'Cairo'),
            border: OutlineInputBorder(borderRadius: BorderRadius.circular(10)),
            suffixIcon: const Icon(Icons.calendar_today, color: AppTheme.primaryColor),
          ),
          child: Text(
            value != null ? _fmtDate(value) : 'اختر التاريخ',
            style: TextStyle(
              fontFamily: 'Cairo',
              color: value != null ? null : Colors.grey,
            ),
          ),
        ),
      ),
    );
  }

  Widget _buildCheckbox(String label, bool value, ValueChanged<bool?> onChanged) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4.0),
      child: CheckboxListTile(
        title: Text(label, style: const TextStyle(fontFamily: 'Cairo')),
        value: value,
        onChanged: (v) => setState(() => onChanged(v)),
        activeColor: AppTheme.primaryColor,
        controlAffinity: ListTileControlAffinity.trailing,
      ),
    );
  }

  Widget _buildSection(String title, List<Widget> children) {
    return ExpansionTile(
      title: Text(title, style: const TextStyle(fontFamily: 'Cairo', fontWeight: FontWeight.bold)),
      initiallyExpanded: true,
      children: children,
    );
  }

  @override
  Widget build(BuildContext context) {
    return Directionality(
      textDirection: TextDirection.rtl,
      child: Scaffold(
        appBar: AppBar(
          title: Text(_receiptId == null ? 'إضافة وصل أمانة' : 'تعديل وصل أمانة',
              style: const TextStyle(fontFamily: 'Cairo')),
          backgroundColor: AppTheme.primaryColor,
          foregroundColor: Colors.white,
        ),
        body: _isLoading
            ? const Center(child: CircularProgressIndicator(color: AppTheme.primaryColor))
            : Form(
                key: _formKey,
                child: ListView(
                  padding: const EdgeInsets.all(16),
                  children: [
                    _buildSection('بيانات العقد', [
                      _buildTextField('contractNumber', 'رقم العقد'),
                      _buildDateField('تاريخ العقد', _contractDate, (d) => _contractDate = d),
                      _buildTextField('contractType', 'نوع العقد'),
                      _buildTextField('contractStatus', 'حالة العقد'),
                      _buildTextField('contractNotes', 'ملاحظات العقد'),
                    ]),
                    _buildSection('الطرف الأول (الشركة)', [
                      _buildTextField('firstPartyName', 'اسم الطرف الأول'),
                      _buildTextField('companyType', 'نوع الشركة'),
                      _buildTextField('companyRepresentativeName', 'ممثل الشركة'),
                      _buildTextField('companyRepresentativeRole', 'صفة الممثل'),
                    ]),
                    _buildSection('الطرف الثاني (المشتري)', [
                      _buildTextField('buyerName', 'اسم المشتري'),
                      _buildTextField('buyerNationalCardNumber', 'رقم البطاقة الوطنية'),
                      _buildTextField('buyerGovernorate', 'المحافظة'),
                      _buildTextField('buyerWorkOrResidenceAddress', 'عنوان السكن/العمل'),
                      _buildTextField('buyerNearestLandmark', 'أقرب نقطة دالة'),
                      _buildTextField('buyerPhoneNumber', 'رقم الهاتف', type: TextInputType.phone),
                      _buildTextField('buyerRationCenterNumber', 'رقم مركز التموين'),
                      _buildTextField('buyerAffiliation', 'الجهة التابع لها'),
                      _buildTextField('buyerMukhtarName', 'اسم المختار'),
                    ]),
                    _buildSection('بيانات البضاعة', [
                      _buildTextField('productType', 'نوع البضاعة'),
                      _buildTextField('productName', 'اسم البضاعة'),
                      _buildTextField('productDescription', 'الوصف'),
                      _buildTextField('productNumber', 'رقم/موديل البضاعة'),
                      _buildTextField('productDeliveryCondition', 'حالة التسليم'),
                    ]),
                    _buildSection('البيانات المالية والتقسيط', [
                      _buildTextField('totalAmountNumber', 'المبلغ الكلي (رقماً)', type: TextInputType.number),
                      _buildTextField('totalAmountText', 'المبلغ الكلي (كتابة)'),
                      _buildTextField('firstInstallmentAmount', 'المقدمة', type: TextInputType.number),
                      _buildDateField('تاريخ المقدمة', _firstInstallmentDate, (d) => _firstInstallmentDate = d),
                      _buildTextField('installmentsCount', 'عدد الأقساط', type: TextInputType.number),
                      _buildTextField('installmentAmount', 'مبلغ القسط الواحد', type: TextInputType.number),
                      _buildDateField('بداية الأقساط', _installmentsStartDate, (d) => _installmentsStartDate = d),
                      _buildDateField('نهاية الأقساط', _installmentsEndDate, (d) => _installmentsEndDate = d),
                      _buildTextField('remainingAmount', 'المبلغ المتبقي', type: TextInputType.number),
                      _buildTextField('paymentMethod', 'طريقة الدفع'),
                    ]),
                    _buildSection('الاستلام والتسليم', [
                      _buildDateField('تاريخ التسليم', _productDeliveryDate, (d) => _productDeliveryDate = d),
                      _buildTextField('deliveryPlace', 'مكان التسليم'),
                      _buildCheckbox('هل تم فحص البضاعة', _isProductInspected, (v) => _isProductInspected = v ?? false),
                      _buildTextField('inspectionNotes', 'ملاحظات الفحص'),
                      _buildCheckbox('تم الاستلام من المشتري', _isReceivedByBuyer, (v) => _isReceivedByBuyer = v ?? false),
                    ]),
                    _buildSection('بيانات وصل الأمانة', [
                      _buildTextField('trustReceiptNumber', 'رقم الوصل'),
                      _buildDateField('تاريخ وصل الأمانة', _trustReceiptDate, (d) => _trustReceiptDate = d),
                      _buildTextField('receiptAmountNumber', 'مبلغ الوصل (رقماً)', type: TextInputType.number),
                      _buildTextField('receiptAmountText', 'مبلغ الوصل (كتابة)'),
                      _buildTextField('receiverName', 'اسم المستلم'),
                      _buildTextField('delivererName', 'اسم المُسلِّم'),
                      _buildTextField('deliveryReason', 'سبب التسليم'),
                      _buildTextField('identityDocumentNumber', 'رقم الهوية'),
                      _buildTextField('address', 'العنوان'),
                      _buildTextField('phoneNumber', 'رقم الهاتف', type: TextInputType.phone),
                    ]),
                    _buildSection('الشهود والتواقيع', [
                      _buildTextField('firstWitnessName', 'الشاهد الأول'),
                      _buildTextField('secondWitnessName', 'الشاهد الثاني'),
                      _buildTextField('salesRepresentativeName', 'مندوب المبيعات'),
                      _buildTextField('cashierName', 'أمين الصندوق'),
                    ]),
                    const SizedBox(height: 20),
                    ElevatedButton(
                      onPressed: _save,
                      style: ElevatedButton.styleFrom(
                        backgroundColor: AppTheme.primaryColor,
                        padding: const EdgeInsets.symmetric(vertical: 15),
                        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(15)),
                      ),
                      child: const Text('حفظ',
                          style: TextStyle(
                              fontFamily: 'Cairo',
                              color: Colors.white,
                              fontSize: 18,
                              fontWeight: FontWeight.bold)),
                    ),
                    const SizedBox(height: 40),
                  ],
                ),
              ),
      ),
    );
  }
}
