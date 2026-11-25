import { reactive } from 'vue'

/** The one path every debtor follows, in order. The whole app is laid out around it. */
export const STAGES = [
  { key: 'Recent', label: 'الاسماء المضافة مؤخرا', hint: 'استُلمت من الشركة وأوراقها قيد التجهيز', icon: 'inbox', color: 'secondary' },
  { key: 'Archive', label: 'ارشيف القانونية', hint: 'جاهزة وتنتظر وقت اقامة الدعوى', icon: 'archive', color: 'info' },
  { key: 'Judicial', label: 'الدعوى القضائية', hint: 'اقامة دعوى، تبليغ، مرافعة…', icon: 'gavel', color: 'warning' },
  { key: 'Decisions', label: 'القرارات', hint: 'صدر القرار وينتظر التنفيذ', icon: 'file-certificate', color: 'primary' },
  { key: 'Execution', label: 'التنفيذ', hint: 'اجراءات التنفيذ والتسديد الدوري', icon: 'hammer', color: 'error' },
]

export const CLOSED = { key: 'Closed', label: 'مغلق', icon: 'circle-check', color: 'success' }

export const stageMeta = key => STAGES.find(s => s.key === key) || CLOSED
export const stageIndex = key => STAGES.findIndex(s => s.key === key)

export function nextStage(key) {
  const i = stageIndex(key)

  return i >= 0 && i < STAGES.length - 1 ? STAGES[i + 1] : null
}

/** What the forward button says for each stage. */
export const MOVE_LABEL = {
  Recent: 'نقل إلى ارشيف القانونية',
  Archive: 'اقامة دعوى قضائية',
  Judicial: 'انتهت الدعوى — إلى القرارات',
  Decisions: 'بدء التنفيذ',
}

/** Which flow's steps a stage logs. */
export const flowOf = stage => stage === 'Judicial' ? 'Judicial' : stage === 'Execution' ? 'Execution' : null

export const CHARGE_KIND = {
  Judicial: 'أجور القانونية',
  Execution: 'أجور التنفيذ',
  EarlySettlement: 'غرامة تسديد كامل',
}

/** Movements of القاصة. The three "fee" kinds move no cash: they pay قلعة الضمان back. */
export const REGISTER_KIND = {
  CompanyFunds: 'استلام من قلعة الضمان',
  JudicialFee: 'تسديد لقلعة الضمان — أجور القانونية',
  ExecutionFee: 'تسديد لقلعة الضمان — تنفيذ',
  EarlySettlementFee: 'تسديد لقلعة الضمان — تسديد قبل الدعوى',
  LawyerTransfer: 'تحويل لمحامٍ',
  LawyerReturn: 'إرجاع من محامٍ',
  Expense: 'صرف',
}

export const EVENT_KIND = {
  Sent: 'استُلم من الشركة',
  Move: 'نقل',
  Close: 'إغلاق',
  Discount: 'خصم',
  Annul: 'ابطال',
  AnnulRequest: 'طلب ابطال',
  AnnulRejected: 'رفض الابطال',
  Unannul: 'إلغاء الابطال',
  Lawyer: 'محامٍ',
  Decision: 'قرار',
  SettlementRequest: 'طلب تسديد كامل',
  SettlementApproved: 'موافقة على التسديد الكامل',
  SettlementRejected: 'رفض التسديد الكامل',
  Payment: 'تسديد',
}

export function money(value) {
  return Math.round(Number(value || 0)).toLocaleString('en-US')
}

function parseUtc(raw) {
  const text = String(raw)

  return new Date(/Z|[+-]\d\d:\d\d$/.test(text) ? text : `${text}Z`)
}

/** Server timestamps named ...AtUtc come without a zone marker. */
export function fromUtc(raw, withTime = false) {
  if (!raw)
    return '—'
  const date = parseUtc(raw)
  if (Number.isNaN(date.getTime()))
    return String(raw).slice(0, 10)
  const dayText = date.toLocaleDateString('en-GB', { timeZone: 'Asia/Baghdad' })

  return withTime
    ? `${dayText} ${date.toLocaleTimeString('en-GB', { timeZone: 'Asia/Baghdad', hour: '2-digit', minute: '2-digit' })}`
    : dayText
}

/** Plain dates (step date, decision date, sale date). */
export function day(raw) {
  const parts = String(raw || '').slice(0, 10).split('-')

  return parts.length === 3 ? `${parts[2]}/${parts[1]}/${parts[0]}` : '—'
}

export function today() {
  const now = new Date()
  const pad = n => String(n).padStart(2, '0')

  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`
}

export function daysSince(rawUtc) {
  if (!rawUtc)
    return 0

  return Math.max(0, Math.floor((Date.now() - parseUtc(rawUtc).getTime()) / 86400000))
}

/** App-wide message bar. */
export const toast = reactive({
  visible: false,
  text: '',
  color: 'success',
  show(text, color = 'success') {
    this.text = text
    this.color = color
    this.visible = true
  },
  ok(text) {
    this.show(text, 'success')
  },
  error(text) {
    this.show(text, 'error')
  },
})
