export const PURCHASE_TYPES = {
  Cash: 'نقد',
  DailyInstallments: 'أقساط يومية',
}

export const ORDER_STATUS = {
  New: { label: 'طلب جديد', color: 'blue', icon: 'mdi-file-document-plus-outline' },
  UnderReview: { label: 'قيد المعالجة', color: 'orange-darken-2', icon: 'mdi-progress-clock' },
  Denied: { label: 'مرفوض', color: 'error', icon: 'mdi-close-circle-outline' },
  Transferred: { label: 'محوّل إلى مندوب المبيعات', color: 'indigo', icon: 'mdi-account-arrow-right-outline' },
  FieldInspection: { label: 'قيد الكشف الميداني', color: 'purple', icon: 'mdi-map-marker-radius-outline' },
  Sold: { label: 'تم البيع', color: 'success', icon: 'mdi-check-decagram' },
  NotSold: { label: 'لم يتم البيع', color: 'grey-darken-1', icon: 'mdi-cancel' },
  Cancelled: { label: 'ملغى', color: 'grey', icon: 'mdi-close' },
}
export const OPEN_ORDER_STATUSES = ['New', 'UnderReview', 'Transferred', 'FieldInspection']

export const COMMISSION_STATUS = {
  Pending: { label: 'معلقة', color: 'orange-darken-2' },
  Available: { label: 'مستحقة للصرف', color: 'success' },
  Paid: { label: 'مصروفة', color: 'primary' },
  Reversed: { label: 'ملغاة', color: 'grey' },
  Frozen: { label: 'مجمدة', color: 'blue-grey' },
}
export const COMMISSION_KIND = {
  Direct: 'مباشرة',
  Network: 'من الشبكة',
  Clawback: 'استرجاع',
  Adjustment: 'تسوية',
}
export const PAYOUT_STATUS = {
  Requested: { label: 'قيد الطلب', color: 'orange-darken-2' },
  Approved: { label: 'موافق عليه', color: 'indigo' },
  Paid: { label: 'مصروف', color: 'success' },
  Rejected: { label: 'مرفوض', color: 'error' },
}
export const PAYOUT_METHODS = [
  { value: 'cash', title: 'نقداً من الفرع' },
  { value: 'zaincash', title: 'زين كاش' },
  { value: 'qicard', title: 'كي كارد' },
]
export const NOTIFICATION_TYPE = {
  Order: { icon: 'mdi-package-variant-closed', color: 'primary' },
  Commission: { icon: 'mdi-cash-multiple', color: 'success' },
  Promotion: { icon: 'mdi-tag-outline', color: 'orange-darken-2' },
  Network: { icon: 'mdi-account-group-outline', color: 'indigo' },
  System: { icon: 'mdi-information-outline', color: 'grey' },
}

export const INSTALLMENTS_NOTICE = 'الأقساط اليومية متاحة فقط لأصحاب المحلات التجارية'
