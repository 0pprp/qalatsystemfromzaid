<script setup>
import { computed, onActivated, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  directorCities,
  directorFollowerManageCreate,
  directorFollowerManageDelete,
  directorFollowerManageGet,
  directorFollowerManageLists,
  directorFollowerManageUpdate,
  directorFollowerOpsCollection,
  directorFollowerOpsDeclining,
  directorFollowerOpsDelegateNotes,
  directorFollowerOpsCancelLegalFollowup,
  directorFollowerOpsLegalFollowups,
  directorFollowerOpsFollowerCards,
  directorFollowerOpsNotes,
  directorFollowerOpsRemainingCustomers,
  directorFollowerOpsSalesWorkspace,
  directorFollowerOpsSetCollectionEnabled,
} from '@/composables/directorApi'
import { useSalesBranches } from '@/composables/useSalesBranches'
import { useToast } from '@/composables/useToast'
import { useUserRole } from '@/composables/useUserRole'

defineOptions({ name: 'DirectorFollowerWork' })
definePage({
  meta: {
    layout: 'default',
    // Stable RouterView key — tab query must NOT remount (would wipe followers).
    keepAlive: true,
  },
})

const VALID_TABS = new Set(['sales', 'declining', 'notes', 'oldCustomers', 'delegateNotes', 'collection'])
const TAB_LABELS = {
  sales: 'مصادقة البيع',
  declining: 'المتدنية',
  notes: 'الملاحظات',
  oldCustomers: 'الزبائن القديمة',
  delegateNotes: 'ملاحظات المندوبين',
  collection: 'الجباية',
}

/** Legacy query tab=field → oldCustomers (قانونية / زبائن قديمة). */
function normalizeTab(raw) {
  const t = String(raw || 'sales')
  if (t === 'field' || t === 'legal')
    return 'oldCustomers'

  return VALID_TABS.has(t) ? t : 'sales'
}

/** Module-level so cache survives accidental remounts. */
const followerCardsCache = new Map()
const detailTabCache = new Map()
const remainingCache = new Map()
const CITY_STORAGE_KEY = 'director-follower-work-city'

const toast = useToast()
const route = useRoute()
const router = useRouter()
const { branches, fetchCities } = useSalesBranches()
const { isDirector, isMonitor, isDirectorOrMonitor } = useUserRole()
const canManageFollowers = computed(() => isDirector.value || isMonitor.value)

const manageDialog = ref(false)
const manageSaving = ref(false)
const manageLoading = ref(false)
const manageEditId = ref(0)
const manageLists = ref([])
const manageForm = ref({
  userName: '',
  password: '',
  displayName: '',
  phoneNumber: '',
  listIds: [],
})

function resetManageForm() {
  manageEditId.value = 0
  manageForm.value = {
    userName: '',
    password: '',
    displayName: '',
    phoneNumber: '',
    listIds: [],
  }
}

async function loadManageLists() {
  const cityValue = cityKey()
  if (!cityValue)
    return
  try {
    const rows = await directorFollowerManageLists({ cityValue })
    manageLists.value = (Array.isArray(rows) ? rows : []).map(r => ({
      listId: Number(r.listId ?? r.ListId ?? 0),
      listName: String(r.listName || r.ListName || r.receiptName || r.ReceiptName || r.listId || r.ListId || ''),
    })).filter(r => r.listId > 0)
  }
  catch (e) {
    manageLists.value = []
    toast.error(softErrorMessage(e, 'تعذر تحميل قوائم المحافظة'))
  }
}

async function openCreateFollower() {
  if (!cityKey()) {
    toast.error('اختر المحافظة أولاً ثم أنشئ المتابع على ذلك الفرع')

    return
  }
  if (!canManageFollowers.value) {
    toast.error('غير مصرح بإدارة المتابعين')

    return
  }
  resetManageForm()
  manageDialog.value = true
  manageLoading.value = true
  await loadManageLists()
  manageLoading.value = false
}

async function openEditFollower(row, ev) {
  ev?.stopPropagation?.()
  const userId = Number(pick(row, 'followerId', 'FollowerId') || 0)
  if (!userId || !cityKey())
    return
  resetManageForm()
  manageEditId.value = userId
  manageDialog.value = true
  manageLoading.value = true
  try {
    await loadManageLists()
    const detail = await directorFollowerManageGet({ cityValue: cityKey(), userId })
    manageForm.value = {
      userName: String(detail?.userName || detail?.UserName || ''),
      password: '',
      displayName: String(detail?.displayName || detail?.DisplayName || ''),
      phoneNumber: String(detail?.phoneNumber || detail?.PhoneNumber || ''),
      listIds: Array.isArray(detail?.listIds)
        ? detail.listIds.map(Number).filter(n => n > 0)
        : (Array.isArray(detail?.ListIds) ? detail.ListIds.map(Number).filter(n => n > 0) : []),
    }
  }
  catch (e) {
    toast.error(softErrorMessage(e, 'تعذر تحميل بيانات المتابع'))
    manageDialog.value = false
  }
  finally {
    manageLoading.value = false
  }
}

async function saveFollowerManage() {
  const cityValue = cityKey()
  if (!cityValue)
    return
  const userName = String(manageForm.value.userName || '').trim()
  const password = String(manageForm.value.password || '')
  if (userName.length < 3) {
    toast.error('اسم المستخدم مطلوب (3 أحرف على الأقل)')

    return
  }
  if (!manageEditId.value && password.length < 4) {
    toast.error('كلمة المرور مطلوبة عند الإنشاء')

    return
  }
  manageSaving.value = true
  try {
    const payload = {
      userName,
      password: password || undefined,
      displayName: userName,
      phoneNumber: String(manageForm.value.phoneNumber || '').trim() || null,
      listIds: Array.isArray(manageForm.value.listIds) ? manageForm.value.listIds.map(Number).filter(n => n > 0) : [],
    }
    if (manageEditId.value) {
      await directorFollowerManageUpdate({ cityValue, userId: manageEditId.value, payload })
      toast.success('تم تحديث المتابع')
    }
    else {
      await directorFollowerManageCreate({ cityValue, payload })
      toast.success('تم إنشاء المتابع')
    }
    manageDialog.value = false
    invalidateCityCache(cityValue)
    await refreshAll()
  }
  catch (e) {
    toast.error(softErrorMessage(e, 'تعذر حفظ المتابع'))
  }
  finally {
    manageSaving.value = false
  }
}

async function deleteFollower(row, ev) {
  ev?.stopPropagation?.()
  const userId = Number(pick(row, 'followerId', 'FollowerId') || 0)
  const name = pick(row, 'followerName', 'FollowerName') || userId
  if (!userId || !cityKey())
    return
  if (!window.confirm(`حذف المتابع «${name}» من هذه المحافظة؟`))
    return
  try {
    await directorFollowerManageDelete({ cityValue: cityKey(), userId })
    toast.success('تم حذف المتابع')
    if (Number(pick(detailFollower.value, 'followerId', 'FollowerId')) === userId)
      closeDetail()
    invalidateCityCache(cityKey())
    await refreshAll()
  }
  catch (e) {
    toast.error(softErrorMessage(e, 'تعذر حذف المتابع'))
  }
}

const pad = n => String(n).padStart(2, '0')

const gatewayCities = ref([])
const selected = ref(null)
const loading = ref(false)
const followerCards = ref([])
const search = ref('')
let cityWatchReady = false
let cardsRequestId = 0

function cityKey() {
  return String(selected.value?.value || '').trim()
}

function detailCacheKey(followerId, tab) {
  return `${cityKey()}:${followerId}:${tab}`
}

function remainingCacheKey(followerId, bucket) {
  return `${cityKey()}:${followerId}:${bucket}`
}

function invalidateCityCache(cityValue) {
  const c = String(cityValue || '').trim()
  if (!c)
    return
  followerCardsCache.delete(c)
  for (const key of [...detailTabCache.keys()]) {
    if (key.startsWith(`${c}:`))
      detailTabCache.delete(key)
  }
  for (const key of [...remainingCache.keys()]) {
    if (key.startsWith(`${c}:`))
      remainingCache.delete(key)
  }
}

function invalidateFollowerDetailCache(followerId, tabs) {
  const fid = Number(followerId || 0)
  if (!fid)
    return
  const list = Array.isArray(tabs) && tabs.length ? tabs : [...VALID_TABS]
  for (const tab of list)
    detailTabCache.delete(detailCacheKey(fid, tab))
  for (const key of [...remainingCache.keys()]) {
    if (key.startsWith(`${cityKey()}:${fid}:`))
      remainingCache.delete(key)
  }
}

const detailFollower = ref(null)
const detailTab = ref('sales')
const detailBusy = ref(false)
const tabLabel = computed(() => TAB_LABELS[detailTab.value] || 'عمل المتابعين')

function readSavedCityValue() {
  try {
    const fromRoute = String(route.query.city || '').trim()
    if (fromRoute)
      return fromRoute

    return String(localStorage.getItem(CITY_STORAGE_KEY) || '').trim()
  }
  catch {
    return String(route.query.city || '').trim()
  }
}

async function persistCity(value) {
  const v = String(value || '').trim()
  if (!v)
    return
  try {
    localStorage.setItem(CITY_STORAGE_KEY, v)
  }
  catch { /* ignore */ }
  const nextQuery = { ...route.query, city: v, tab: route.query.tab || detailTab.value }
  if (String(route.query.city || '') !== v || !route.query.tab) {
    try {
      await router.replace({ query: nextQuery })
    }
    catch { /* ignore */ }
  }
}

function syncTabFromRoute() {
  detailTab.value = normalizeTab(route.query.tab)
}

function errStatus(e) {
  return Number(e?.response?.status || e?.status || 0)
}

function softErrorMessage(e, fallback) {
  const st = errStatus(e)
  if (st === 504 || st === 408 || /timeout|network|504/i.test(String(e?.message || '')))
    return 'الاتصال بطيء — أبقِ المحافظة كما هي وحاول تحديث القائمة'

  return e?.response?.data?.message || e?.message || fallback
}

const salesRows = ref([])
const decliningRows = ref([])
const noteRows = ref([])
const oldCustomerRows = ref([])
const delegateNoteRows = ref([])
const collectionBundle = ref({ lists: [], payments: [] })
const workBundle = ref({
  listCards: [],
  ratingBuckets: [],
  ratedCustomerCount: 0,
  remainingLe100Count: 0,
  remainingLt200Count: 0,
  remainingLe100Trend: 'flat',
  remainingLt200Trend: 'flat',
})
const remainingBucket = ref(null)
const remainingRows = ref([])
const remainingBusy = ref(false)
const toggleBusyId = ref(0)
const profileRow = ref(null)

const EXCLUDED_CITY_VALUES = new Set(['7', '15', '16', '18'])

function isExcludedCity(c) {
  const v = String(c?.value || c?.Value || '').trim()
  if (EXCLUDED_CITY_VALUES.has(v))
    return true
  const n = String(c?.name || c?.Name || '')

  return /عقيل|منهج|المنهاج|كوت|ميسان/.test(n)
}

const cityCards = computed(() => {
  const fromGw = Array.isArray(gatewayCities.value) ? gatewayCities.value : []
  const fromBranches = (branches.value || []).map(b => ({
    value: String(b.value ?? b.Value ?? ''),
    name: b.name || b.Name || '',
  }))
  const map = new Map()
  for (const c of [...fromGw, ...fromBranches]) {
    const v = String(c.value || c.Value || '').trim()
    if (!v || isExcludedCity(c))
      continue
    if (!map.has(v))
      map.set(v, { value: v, name: c.name || c.Name || v })
  }

  return [...map.values()].sort((a, b) => a.name.localeCompare(b.name, 'ar'))
})

const filteredFollowers = computed(() => {
  const q = search.value.trim()
  let rows = Array.isArray(followerCards.value) ? followerCards.value : []
  if (q) {
    rows = rows.filter(r =>
      String(r.followerName || r.FollowerName || '').includes(q)
      || String(r.followerId || r.FollowerId || '').includes(q))
  }

  return rows
})

const listCards = computed(() => {
  const b = workBundle.value || {}

  return Array.isArray(b.listCards) ? b.listCards : (Array.isArray(b.ListCards) ? b.ListCards : [])
})

const ratingBuckets = computed(() => {
  const b = workBundle.value || {}
  const rows = Array.isArray(b.ratingBuckets)
    ? b.ratingBuckets
    : (Array.isArray(b.RatingBuckets) ? b.RatingBuckets : [])
  if (rows.length)
    return rows

  return [
    { levelKey: 'excellent', label: 'ممتاز', customerCount: 0 },
    { levelKey: 'good', label: 'جيد', customerCount: 0 },
    { levelKey: 'weak', label: 'ضعيف', customerCount: 0 },
    { levelKey: 'lagging', label: 'متلكئ', customerCount: 0 },
    { levelKey: 'bad', label: 'سيء', customerCount: 0 },
  ]
})

const pendingSalesCount = computed(() =>
  (salesRows.value || []).filter(r => !(r.authenticated ?? r.Authenticated)).length)

const authenticatedSalesCount = computed(() =>
  (salesRows.value || []).filter(r => !!(r.authenticated ?? r.Authenticated)).length)

const detailOpen = computed(() => !!detailFollower.value)

function pick(obj, ...keys) {
  if (!obj)
    return undefined
  for (const key of keys) {
    if (obj[key] != null && obj[key] !== '')
      return obj[key]
  }

  return undefined
}

function fmt(n) {
  const v = Number(n || 0)
  if (!Number.isFinite(v) || v === 0)
    return '—'

  return new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 }).format(Math.round(v))
}

function fmtRating(n) {
  const v = Number(n)
  if (!Number.isFinite(v))
    return '—'
  // Match follower app display: whole number or 1 decimal
  if (Math.abs(v - Math.round(v)) < 1e-9)
    return String(Math.round(v))

  return new Intl.NumberFormat('en-US', { maximumFractionDigits: 1, minimumFractionDigits: 1 }).format(v)
}

function trendKey(row, ...keys) {
  const raw = String(pick(row, ...keys) || 'flat').toLowerCase()
  if (raw === 'up' || raw === 'down')
    return raw

  return 'flat'
}

function trendIcon(trend) {
  if (trend === 'up')
    return 'tabler-arrow-up'
  if (trend === 'down')
    return 'tabler-arrow-down'

  return 'tabler-equal'
}

function trendClass(trend) {
  if (trend === 'up')
    return 'dfw-trend-up'
  if (trend === 'down')
    return 'dfw-trend-down'

  return 'dfw-trend-flat'
}

function fmtDate(isoStr) {
  if (!isoStr)
    return '—'
  const d = new Date(isoStr)
  if (Number.isNaN(d.getTime()))
    return '—'
  const iraq = new Date(d.getTime() + 3 * 60 * 60 * 1000)

  return `${iraq.getUTCFullYear()}-${pad(iraq.getUTCMonth() + 1)}-${pad(iraq.getUTCDate())}`
}

function fmtDateTime(isoStr) {
  if (!isoStr)
    return '—'
  const d = new Date(isoStr)
  if (Number.isNaN(d.getTime()))
    return '—'
  const iraq = new Date(d.getTime() + 3 * 60 * 60 * 1000)

  return `${iraq.getUTCFullYear()}-${pad(iraq.getUTCMonth() + 1)}-${pad(iraq.getUTCDate())} ${pad(iraq.getUTCHours())}:${pad(iraq.getUTCMinutes())}`
}

/** مصادق أحمر إذا تقييم المتابع أو المندوب ≤ 6 */
function isLowAuthRisk(row) {
  if (!row || !(row.authenticated ?? row.Authenticated))
    return false
  const auth = Number(pick(row, 'authRating', 'AuthRating'))
  const shop = Number(pick(row, 'shopRating', 'ShopRating'))
  const lowAuth = Number.isFinite(auth) && auth >= 1 && auth <= 6
  const lowShop = Number.isFinite(shop) && shop >= 1 && shop <= 6

  return lowAuth || lowShop
}

function salesAuthBadgeClass(row) {
  if (!(row.authenticated ?? row.Authenticated))
    return 'dfw-badge-warn'
  if (isLowAuthRisk(row))
    return 'dfw-badge-danger'

  return 'dfw-badge-on'
}

function openCustomerProfile(row) {
  profileRow.value = row
}

function closeProfile() {
  profileRow.value = null
}

function profileCustomerId(row = profileRow.value) {
  return Number(pick(row, 'customerId', 'CustomerId', 'customerID', 'CustomerID') || 0)
}

function openFullCustomerProfile() {
  const cid = profileCustomerId()
  const city = cityKey()
  const fid = Number(pick(detailFollower.value, 'followerId', 'FollowerId') || 0)
  if (!cid || !city) {
    toast.warning('تعذر فتح بروفايل الزبون')

    return
  }
  router.push({
    path: '/sales-manager-customer-profile',
    query: {
      cityValue: city,
      customerId: String(cid),
      name: pick(profileRow.value, 'customerName', 'CustomerName') || '',
      phone: pick(profileRow.value, 'phone', 'Phone') || '',
      returnTo: 'director-follower-work',
      followerId: fid ? String(fid) : undefined,
      tab: detailTab.value || 'sales',
      resumeCustomerId: String(cid),
    },
  })
}

function findRowByCustomerId(customerId) {
  const id = Number(customerId || 0)
  if (!id)
    return null
  const pools = [
    salesRows.value,
    decliningRows.value,
    noteRows.value,
    oldCustomerRows.value,
    remainingRows.value,
    delegateNoteRows.value,
  ]
  for (const rows of pools) {
    const hit = (rows || []).find(r => Number(pick(r, 'customerId', 'CustomerId', 'customerID', 'CustomerID') || 0) === id)
    if (hit)
      return hit
  }

  return null
}

async function resumeFromRoute() {
  const resumeCid = Number(route.query.resumeCustomerId || 0)
  const resumeFid = Number(route.query.followerId || 0)
  if (!resumeCid || !resumeFid)
    return false

  resumeInProgress = true
  try {
    const city = String(route.query.city || '').trim()
    if (city && String(selected.value?.value || '') !== city) {
      const match = cityCards.value.find(c => String(c.value) === city)
      if (!match)
        return false
      selected.value = match
      await persistCity(city)
      await loadFollowerCards({ force: true })
    }

    const tab = normalizeTab(route.query.tab)
    let follower = followerCards.value.find(
      r => Number(pick(r, 'followerId', 'FollowerId') || 0) === resumeFid,
    )
    if (!follower) {
      await loadFollowerCards({ force: true })
      follower = followerCards.value.find(
        r => Number(pick(r, 'followerId', 'FollowerId') || 0) === resumeFid,
      )
    }
    if (!follower)
      return false

    await openDetail(follower, tab)
    const row = findRowByCustomerId(resumeCid)
    if (row)
      profileRow.value = row
    else
      profileRow.value = { customerId: resumeCid, CustomerId: resumeCid }

    const nextQuery = { ...route.query }
    delete nextQuery.resumeCustomerId
    delete nextQuery.followerId
    delete nextQuery.returnTo
    await router.replace({ query: nextQuery }).catch(() => {})

    return true
  }
  finally {
    resumeInProgress = false
  }
}

const cancelLegalOpen = ref(false)
const cancelLegalBusy = ref(false)
const cancelLegalReason = ref('')

function canCancelOldCustomer(row) {
  if (!row || !canManageFollowers.value)
    return false
  if (row.isDone ?? row.IsDone)
    return false
  const status = String(pick(row, 'status', 'Status') || '')

  return !status || status === 'InProgress' || status === 'قيد العمل'
}

function openCancelLegalDialog() {
  if (!canCancelOldCustomer(profileRow.value))
    return
  cancelLegalReason.value = ''
  cancelLegalOpen.value = true
}

async function submitCancelLegal() {
  const row = profileRow.value
  const caseId = Number(pick(row, 'id', 'Id') || 0)
  const cityValue = cityKey()
  const reason = (cancelLegalReason.value || '').trim()
  if (!caseId || !cityValue) {
    toast.error('بيانات المتابعة غير مكتملة')

    return
  }
  if (!reason) {
    toast.error('سبب الإلغاء مطلوب')

    return
  }
  cancelLegalBusy.value = true
  try {
    await directorFollowerOpsCancelLegalFollowup({ cityValue, caseId, reason })
    toast.success('تم الإلغاء — الطلب عاد إلى غير مسند')
    cancelLegalOpen.value = false
    closeProfile()
    const fid = Number(pick(detailFollower.value, 'followerId', 'FollowerId') || 0)
    invalidateFollowerDetailCache(fid, ['oldCustomers'])
    await loadDetail({ force: true })
  }
  catch (e) {
    console.error(e)
    toast.error(e?.response?.data?.message || e?.message || 'تعذر إلغاء الإرسال')
  }
  finally {
    cancelLegalBusy.value = false
  }
}

function resetDetailData() {
  salesRows.value = []
  decliningRows.value = []
  noteRows.value = []
  oldCustomerRows.value = []
  delegateNoteRows.value = []
  collectionBundle.value = { lists: [], payments: [] }
  workBundle.value = emptyWorkBundle()
  remainingBucket.value = null
  remainingRows.value = []
  profileRow.value = null
}

function closeDetail() {
  detailFollower.value = null
  resetDetailData()
}

async function loadCities() {
  try {
    await fetchCities()
  }
  catch { /* branches optional */ }
  try {
    gatewayCities.value = await directorCities() || []
  }
  catch (e) {
    console.error(e)
    gatewayCities.value = []
  }
  const saved = readSavedCityValue()
  const match = cityCards.value.find(c => String(c.value) === saved)
  if (match)
    selected.value = match
  else if (!selected.value && cityCards.value.length)
    selected.value = cityCards.value[0]
  if (selected.value?.value)
    await persistCity(selected.value.value)
}

function emptyWorkBundle() {
  return {
    listCards: [],
    ratingBuckets: [],
    ratedCustomerCount: 0,
    remainingLe100Count: 0,
    remainingLt200Count: 0,
    remainingLe100Trend: 'flat',
    remainingLt200Trend: 'flat',
  }
}

function normalizeWorkBundle(b) {
  const src = b || {}

  return {
    listCards: src.listCards || src.ListCards || [],
    ratingBuckets: src.ratingBuckets || src.RatingBuckets || [],
    ratedCustomerCount: src.ratedCustomerCount ?? src.RatedCustomerCount ?? 0,
    remainingLe100Count: src.remainingLe100Count ?? src.RemainingLe100Count ?? 0,
    remainingLt200Count: src.remainingLt200Count ?? src.RemainingLt200Count ?? 0,
    remainingLe100Trend: src.remainingLe100Trend || src.RemainingLe100Trend || 'flat',
    remainingLt200Trend: src.remainingLt200Trend || src.RemainingLt200Trend || 'flat',
  }
}

function applyCachedDetail(payload) {
  if (!payload || typeof payload !== 'object')
    return false
  salesRows.value = payload.salesRows || []
  decliningRows.value = payload.decliningRows || []
  noteRows.value = payload.noteRows || []
  oldCustomerRows.value = payload.oldCustomerRows || []
  delegateNoteRows.value = payload.delegateNoteRows || []
  collectionBundle.value = payload.collectionBundle || { lists: [], payments: [] }
  workBundle.value = payload.workBundle || emptyWorkBundle()
  remainingBucket.value = null
  remainingRows.value = []
  profileRow.value = null

  return true
}

function snapshotDetailForCache(tab) {
  if (tab === 'sales') {
    return {
      salesRows: salesRows.value,
      workBundle: workBundle.value,
    }
  }
  if (tab === 'declining')
    return { decliningRows: decliningRows.value }
  if (tab === 'notes')
    return { noteRows: noteRows.value }
  if (tab === 'oldCustomers')
    return { oldCustomerRows: oldCustomerRows.value }
  if (tab === 'delegateNotes')
    return { delegateNoteRows: delegateNoteRows.value }

  return { collectionBundle: collectionBundle.value }
}

async function loadFollowerCards({ silent, force } = {}) {
  if (!selected.value?.value)
    return
  const ck = cityKey()
  if (!force && followerCardsCache.has(ck)) {
    followerCards.value = followerCardsCache.get(ck) || []
    if (!silent)
      loading.value = false

    return
  }
  const reqId = ++cardsRequestId
  if (!silent)
    loading.value = true
  try {
    const rows = await directorFollowerOpsFollowerCards({
      cityValue: selected.value.value,
    }) || []
    if (reqId !== cardsRequestId)
      return
    followerCards.value = rows
    followerCardsCache.set(ck, rows)
  }
  catch (e) {
    console.error(e)
    if (reqId !== cardsRequestId)
      return
    // Keep previous cards on timeout/504 so tab switches stay usable.
    if (!followerCards.value.length)
      followerCards.value = []
    toast.error(softErrorMessage(e, 'تعذر تحميل كروت المتابعين'))
  }
  finally {
    if (reqId === cardsRequestId)
      loading.value = false
  }
}

async function openDetail(row, tab) {
  detailFollower.value = row
  const nextTab = VALID_TABS.has(tab) ? tab : detailTab.value
  detailTab.value = nextTab
  if (String(route.query.tab || '') !== nextTab)
    await router.replace({ query: { ...route.query, tab: nextTab } })
  await loadDetail()
  requestAnimationFrame(() => {
    document.getElementById('dfw-detail')?.scrollIntoView({ behavior: 'smooth', block: 'start' })
  })
}

async function loadDetail({ force } = {}) {
  const fid = Number(pick(detailFollower.value, 'followerId', 'FollowerId') || 0)
  if (!fid || !selected.value?.value)
    return
  const tab = detailTab.value
  const cacheKey = detailCacheKey(fid, tab)
  if (!force && detailTabCache.has(cacheKey)) {
    applyCachedDetail(detailTabCache.get(cacheKey))
    detailBusy.value = false

    return
  }
  detailBusy.value = true
  salesRows.value = []
  decliningRows.value = []
  noteRows.value = []
  oldCustomerRows.value = []
  delegateNoteRows.value = []
  collectionBundle.value = { lists: [], payments: [] }
  workBundle.value = emptyWorkBundle()
  remainingBucket.value = null
  remainingRows.value = []
  try {
    const cityValue = selected.value.value
    if (tab === 'sales') {
      // One gateway hop: sales-auth + work-bundle together
      const ws = await directorFollowerOpsSalesWorkspace({ cityValue, followerId: fid }) || {}
      salesRows.value = ws.salesAuth || ws.SalesAuth || []
      workBundle.value = normalizeWorkBundle(ws.bundle || ws.Bundle || {})
    }
    else if (tab === 'declining') {
      decliningRows.value = await directorFollowerOpsDeclining({ cityValue, followerId: fid }) || []
    }
    else if (tab === 'notes') {
      noteRows.value = await directorFollowerOpsNotes({ cityValue, followerId: fid }) || []
    }
    else if (tab === 'oldCustomers') {
      oldCustomerRows.value = await directorFollowerOpsLegalFollowups({ cityValue, followerId: fid }) || []
    }
    else if (tab === 'delegateNotes') {
      delegateNoteRows.value = await directorFollowerOpsDelegateNotes({ cityValue, followerId: fid }) || []
    }
    else {
      const bundle = await directorFollowerOpsCollection({ cityValue, followerId: fid }) || {}
      collectionBundle.value = {
        lists: bundle.lists || bundle.Lists || [],
        payments: bundle.payments || bundle.Payments || [],
      }
    }
    detailTabCache.set(cacheKey, snapshotDetailForCache(tab))
  }
  catch (e) {
    console.error(e)
    toast.error(softErrorMessage(e, 'تعذر تحميل التفاصيل'))
  }
  finally {
    detailBusy.value = false
  }
}

async function openRemainingBucket(bucket) {
  const fid = Number(pick(detailFollower.value, 'followerId', 'FollowerId') || 0)
  if (!fid || !selected.value?.value)
    return
  if (remainingBucket.value === bucket) {
    remainingBucket.value = null
    remainingRows.value = []

    return
  }
  remainingBucket.value = bucket
  remainingBusy.value = true
  remainingRows.value = []
  profileRow.value = null
  const rk = remainingCacheKey(fid, bucket)
  if (remainingCache.has(rk)) {
    remainingRows.value = remainingCache.get(rk) || []
    remainingBusy.value = false

    return
  }
  try {
    remainingRows.value = await directorFollowerOpsRemainingCustomers({
      cityValue: selected.value.value,
      followerId: fid,
      bucket,
    }) || []
    remainingCache.set(rk, remainingRows.value)
  }
  catch (e) {
    console.error(e)
    toast.error(e?.response?.data?.message || e?.message || 'تعذر تحميل زبائن القائمة')
    remainingRows.value = []
  }
  finally {
    remainingBusy.value = false
  }
}

async function toggleCollection(list, enabled) {
  const listId = Number(pick(list, 'listId', 'ListId') || 0)
  if (!listId || !selected.value?.value)
    return
  toggleBusyId.value = listId
  try {
    await directorFollowerOpsSetCollectionEnabled({
      cityValue: selected.value.value,
      listId,
      enabled: !!enabled,
    })
    list.followerCollectionEnabled = !!enabled
    list.FollowerCollectionEnabled = !!enabled
    toast.success(enabled ? 'تم تفعيل الجباية' : 'تم إيقاف الجباية')
    const fid = Number(pick(detailFollower.value, 'followerId', 'FollowerId') || 0)
    invalidateFollowerDetailCache(fid, ['collection'])
    await loadDetail({ force: true })
  }
  catch (e) {
    console.error(e)
    toast.error(e?.response?.data?.message || e?.message || 'تعذر تحديث الجباية')
  }
  finally {
    toggleBusyId.value = 0
  }
}

async function refreshAll() {
  invalidateCityCache(cityKey())
  await loadFollowerCards({ force: true })
  if (detailOpen.value)
    await loadDetail({ force: true })
}

watch(() => route.query.tab, () => {
  const prev = detailTab.value
  syncTabFromRoute()
  // Sidebar often navigates with { tab } only — restore city into the URL.
  const saved = readSavedCityValue()
  if (saved && String(route.query.city || '') !== saved) {
    router.replace({
      query: { ...route.query, tab: detailTab.value, city: saved },
    }).catch(() => {})
  }
  if (saved && cityCards.value.length) {
    const match = cityCards.value.find(c => String(c.value) === saved)
    if (match && String(selected.value?.value || '') !== saved)
      selected.value = match
  }
  // Keep governorate; only reload open follower detail for the new section.
  if (detailOpen.value && detailTab.value !== prev) {
    profileRow.value = null
    loadDetail()
  }
})

let resumeInProgress = false

watch(selected, async (v, old) => {
  if (!cityWatchReady || !v?.value || resumeInProgress)
    return
  await persistCity(v.value)
  if (old?.value && String(old.value) === String(v.value))
    return
  closeDetail()
  await loadFollowerCards()
})

async function ensureCityQuery() {
  await router.replace({
    query: {
      ...route.query,
      tab: route.query.tab || detailTab.value || 'sales',
      ...(selected.value?.value ? { city: selected.value.value } : {}),
    },
  }).catch(() => {})
}

onMounted(async () => {
  syncTabFromRoute()
  await loadCities()
  cityWatchReady = true
  await ensureCityQuery()
  // Hydrate from module cache first (no spinner) when KeepAlive/remount races.
  const ck = cityKey()
  if (ck && followerCardsCache.has(ck) && followerCardsCache.get(ck)?.length) {
    followerCards.value = followerCardsCache.get(ck)
    loading.value = false
    await resumeFromRoute()

    return
  }
  await loadFollowerCards()
  await resumeFromRoute()
})

onActivated(async () => {
  const prev = detailTab.value
  syncTabFromRoute()
  await ensureCityQuery()
  if (await resumeFromRoute())
    return
  if (detailOpen.value && detailTab.value !== prev)
    await loadDetail()
})
</script>

<template>
  <div class="dfw-page">
    <div class="dfw-toolbar">
      <div class="dfw-title-block">
        <div class="dfw-title">
          <VIcon
            icon="tabler-user-scan"
            size="26"
            class="dfw-title-icon"
          />
          <span>عمل المتابعين</span>
          <span class="dfw-tab-chip">{{ tabLabel }}</span>
        </div>
        <div class="dfw-subtitle">
          اختر المحافظة ثم المتابع من الكروت — القسم من السايدبار تحت «عمل المتابعين»
        </div>
      </div>

      <div class="dfw-filters">
        <VSelect
          v-model="selected"
          :items="cityCards"
          item-title="name"
          item-value="value"
          return-object
          label="المحافظة"
          density="comfortable"
          hide-details
          class="dfw-select dfw-select-city"
        />
        <VTextField
          v-model="search"
          label="بحث متابع"
          density="comfortable"
          hide-details
          clearable
          prepend-inner-icon="tabler-search"
          class="dfw-select dfw-select-search"
        />
        <VBtn
          v-if="canManageFollowers"
          color="primary"
          variant="flat"
          prepend-icon="tabler-user-plus"
          :disabled="!cityKey()"
          @click="openCreateFollower"
        >
          إنشاء متابع
        </VBtn>
        <VBtn
          class="dfw-refresh"
          :loading="loading"
          @click="refreshAll"
        >
          تحديث
        </VBtn>
      </div>
    </div>

    <div class="dfw-stats">
      <div class="dfw-stat">
        <span class="dfw-stat-label">المتابعون</span>
        <span class="dfw-stat-value">{{ filteredFollowers.length }}</span>
      </div>
    </div>

    <div
      v-if="loading"
      class="dfw-loading"
    >
      <VProgressCircular
        indeterminate
        color="primary"
      />
    </div>

    <div
      v-else
      class="dfw-follower-grid"
    >
      <button
        v-for="row in filteredFollowers"
        :key="pick(row, 'followerId', 'FollowerId')"
        type="button"
        class="dfw-follower-card"
        :class="{
          'dfw-follower-card-active': Number(pick(detailFollower, 'followerId', 'FollowerId')) === Number(pick(row, 'followerId', 'FollowerId')),
        }"
        @click="openDetail(row, detailTab)"
      >
        <div class="dfw-follower-card-name">
          <span>{{ pick(row, 'followerName', 'FollowerName') }}</span>
          <span
            v-if="canManageFollowers"
            class="dfw-card-actions"
            @click.stop
          >
            <VBtn
              icon="tabler-edit"
              size="x-small"
              variant="text"
              @click="openEditFollower(row, $event)"
            />
            <VBtn
              icon="tabler-trash"
              size="x-small"
              variant="text"
              color="error"
              @click="deleteFollower(row, $event)"
            />
          </span>
        </div>
        <div class="dfw-follower-card-meta">
          <div class="dfw-meta-block">
            <span>التقييم الوهمي</span>
            <b class="dfw-rating-with-trend">
              {{ fmtRating(pick(row, 'phantomRating', 'PhantomRating')) }}
              <VIcon
                :icon="trendIcon(trendKey(row, 'ratingTrend', 'RatingTrend'))"
                size="16"
                :class="trendClass(trendKey(row, 'ratingTrend', 'RatingTrend'))"
              />
            </b>
          </div>
          <div class="dfw-meta-block">
            <span>زبائن التقييم</span>
            <b>{{ fmt(pick(row, 'ratedCustomerCount', 'RatedCustomerCount')) }}</b>
          </div>
        </div>
      </button>
      <div
        v-if="!filteredFollowers.length"
        class="dfw-empty-block"
      >
        لا يوجد متابعون لهذه المحافظة
      </div>
    </div>

    <div
      v-if="detailOpen"
      id="dfw-detail"
      class="dfw-detail"
    >
      <div class="dfw-detail-head">
        <div>
          <div class="dfw-detail-title">
            {{ pick(detailFollower, 'followerName', 'FollowerName') || 'تفاصيل المتابع' }}
            <span class="dfw-tab-chip">{{ tabLabel }}</span>
          </div>
          <div class="dfw-detail-sub">
            غيّر القسم من السايدبار تحت عمل المتابعين
          </div>
        </div>
        <VBtn
          variant="text"
          @click="closeDetail"
        >
          إغلاق التفاصيل
        </VBtn>
      </div>

      <div class="dfw-detail-body">
        <div
          v-if="detailBusy"
          class="dfw-loading"
        >
          <VProgressCircular
            indeterminate
            color="primary"
          />
        </div>

        <template v-else-if="detailTab === 'sales'">
          <div class="dfw-section-label">
            كروت القوائم
          </div>
          <div class="dfw-list-grid">
            <div
              v-for="lc in listCards"
              :key="pick(lc, 'listId', 'ListId')"
              class="dfw-list-card dfw-list-card-static"
            >
              <div class="dfw-list-card-name">
                {{ pick(lc, 'listName', 'ListName') || `قائمة ${pick(lc, 'listId', 'ListId')}` }}
              </div>
              <div class="dfw-follower-card-meta">
                <div class="dfw-meta-block">
                  <span>التقييم الوهمي</span>
                  <b class="dfw-rating-with-trend">
                    {{ fmtRating(pick(lc, 'phantomRating', 'PhantomRating')) }}
                    <VIcon
                      :icon="trendIcon(trendKey(lc, 'ratingTrend', 'RatingTrend'))"
                      size="16"
                      :class="trendClass(trendKey(lc, 'ratingTrend', 'RatingTrend'))"
                    />
                  </b>
                </div>
                <div class="dfw-meta-block">
                  <span>زبائن التقييم</span>
                  <b>{{ fmt(pick(lc, 'ratedCustomerCount', 'RatedCustomerCount')) }}</b>
                </div>
              </div>
            </div>
            <div
              v-if="!listCards.length"
              class="dfw-empty-block"
            >
              لا قوائم مسندة
            </div>
          </div>

          <div class="dfw-section-label mt-4">
            كروت التقييمات
          </div>
          <div class="dfw-bucket-grid">
            <button
              v-for="rb in ratingBuckets"
              :key="pick(rb, 'levelKey', 'LevelKey')"
              type="button"
              class="dfw-bucket-card"
              :class="{ 'dfw-bucket-card-active': remainingBucket === pick(rb, 'levelKey', 'LevelKey') }"
              @click="openRemainingBucket(pick(rb, 'levelKey', 'LevelKey'))"
            >
              <span>{{ pick(rb, 'label', 'Label') }}</span>
              <b class="dfw-rating-with-trend">
                {{ fmt(pick(rb, 'customerCount', 'CustomerCount')) }}
                <VIcon
                  :icon="trendIcon(trendKey(rb, 'countTrend', 'CountTrend'))"
                  size="16"
                  :class="trendClass(trendKey(rb, 'countTrend', 'CountTrend'))"
                />
              </b>
            </button>
          </div>

          <div class="dfw-section-label mt-4">
            كروت المتبقي
          </div>
          <div class="dfw-bucket-grid">
            <button
              type="button"
              class="dfw-bucket-card"
              :class="{ 'dfw-bucket-card-active': remainingBucket === 'le100' }"
              @click="openRemainingBucket('le100')"
            >
              <span>باقي أقل من 100,000</span>
              <b class="dfw-rating-with-trend">
                {{ fmt(workBundle.remainingLe100Count) }}
                <VIcon
                  :icon="trendIcon(trendKey(workBundle, 'remainingLe100Trend', 'RemainingLe100Trend'))"
                  size="16"
                  :class="trendClass(trendKey(workBundle, 'remainingLe100Trend', 'RemainingLe100Trend'))"
                />
              </b>
            </button>
            <button
              type="button"
              class="dfw-bucket-card"
              :class="{ 'dfw-bucket-card-active': remainingBucket === 'lt200' }"
              @click="openRemainingBucket('lt200')"
            >
              <span>باقي من 100,000 إلى أقل من 200,000</span>
              <b class="dfw-rating-with-trend">
                {{ fmt(workBundle.remainingLt200Count) }}
                <VIcon
                  :icon="trendIcon(trendKey(workBundle, 'remainingLt200Trend', 'RemainingLt200Trend'))"
                  size="16"
                  :class="trendClass(trendKey(workBundle, 'remainingLt200Trend', 'RemainingLt200Trend'))"
                />
              </b>
            </button>
          </div>

          <div
            v-if="remainingBucket"
            class="dfw-panel"
          >
            <div class="dfw-section-label">
              أسماء الزبائن
            </div>
            <div
              v-if="remainingBusy"
              class="dfw-loading dfw-loading-sm"
            >
              <VProgressCircular
                indeterminate
                color="primary"
                size="28"
              />
            </div>
            <div
              v-else
              class="dfw-name-grid"
            >
              <button
                v-for="(r, i) in remainingRows"
                :key="`rem-${i}`"
                type="button"
                class="dfw-name-card"
                @click="openCustomerProfile(r)"
              >
                <div class="dfw-name-card-title">
                  {{ pick(r, 'customerName', 'CustomerName') }}
                </div>
                <div class="dfw-name-card-sub">
                  {{ pick(r, 'listName', 'ListName') || '—' }}
                  · {{ pick(r, 'ratingLabel', 'RatingLabel') || '—' }}
                  · متبقي {{ fmt(pick(r, 'amountRemaining', 'AmountRemaining')) }}
                </div>
              </button>
              <div
                v-if="!remainingRows.length"
                class="dfw-empty-block"
              >
                لا زبائن في هذه القائمة
              </div>
            </div>
          </div>

          <div
            v-if="profileRow"
            class="dfw-profile"
          >
            <div class="dfw-profile-head">
              <div class="dfw-profile-title">
                بروفايل الزبون — {{ pick(profileRow, 'customerName', 'CustomerName') }}
                <span
                  v-if="pick(profileRow, 'authenticated', 'Authenticated') != null"
                  class="dfw-badge"
                  :class="salesAuthBadgeClass(profileRow)"
                >
                  {{ (profileRow.authenticated ?? profileRow.Authenticated) ? 'مصادق' : 'بانتظار' }}
                </span>
                <button
                  v-if="profileCustomerId()"
                  type="button"
                  class="dfw-full-profile-btn"
                  @click="openFullCustomerProfile"
                >
                  عرض بروفايل الزبون الكامل
                </button>
              </div>
              <VBtn
                size="small"
                variant="text"
                @click="closeProfile"
              >
                إغلاق البروفايل
              </VBtn>
            </div>
            <div class="dfw-profile-containers">
              <section class="dfw-profile-box">
                <h3 class="dfw-profile-box-title">
                  معلومات عامة
                </h3>
                <div class="dfw-profile-grid dfw-profile-grid-stack">
                  <div><span>الهاتف</span><b>{{ pick(profileRow, 'phone', 'Phone') || '—' }}</b></div>
                  <div><span>القائمة</span><b>{{ pick(profileRow, 'listName', 'ListName') || '—' }}</b></div>
                  <div v-if="pick(profileRow, 'authenticated', 'Authenticated') != null">
                    <span>الحالة</span>
                    <b>{{ (profileRow.authenticated ?? profileRow.Authenticated) ? 'مصادق' : 'بانتظار' }}</b>
                  </div>
                  <div v-if="pick(profileRow, 'currentRatingLabel', 'CurrentRatingLabel') || pick(profileRow, 'ratingLabel', 'RatingLabel')">
                    <span>التقييم</span>
                    <b>{{ pick(profileRow, 'currentRatingLabel', 'CurrentRatingLabel') || pick(profileRow, 'ratingLabel', 'RatingLabel') }}</b>
                  </div>
                  <div v-if="pick(profileRow, 'salePrice', 'SalePrice') != null">
                    <span>سعر البيع</span>
                    <b>{{ fmt(pick(profileRow, 'salePrice', 'SalePrice')) }}</b>
                  </div>
                  <div v-if="pick(profileRow, 'saleDate', 'SaleDate')">
                    <span>تاريخ البيع</span>
                    <b>{{ fmtDate(pick(profileRow, 'saleDate', 'SaleDate')) }}</b>
                  </div>
                  <div v-if="pick(profileRow, 'dailyInstallment', 'DailyInstallment') != null">
                    <span>القسط</span>
                    <b>{{ fmt(pick(profileRow, 'dailyInstallment', 'DailyInstallment')) }}</b>
                  </div>
                  <div v-if="pick(profileRow, 'sellerName', 'SellerName')">
                    <span>اسم البائع</span>
                    <b>{{ pick(profileRow, 'sellerName', 'SellerName') }}</b>
                  </div>
                  <div v-if="pick(profileRow, 'paidAmount', 'PaidAmount') != null">
                    <span>واصل</span>
                    <b>{{ fmt(pick(profileRow, 'paidAmount', 'PaidAmount')) }}</b>
                  </div>
                  <div v-if="pick(profileRow, 'remainingAmount', 'RemainingAmount') != null || pick(profileRow, 'amountRemaining', 'AmountRemaining') != null">
                    <span>متبقي</span>
                    <b>{{ fmt(pick(profileRow, 'remainingAmount', 'RemainingAmount') ?? pick(profileRow, 'amountRemaining', 'AmountRemaining')) }}</b>
                  </div>
                </div>
              </section>

              <section class="dfw-profile-box">
                <h3 class="dfw-profile-box-title">
                  تقييم المتابع
                </h3>
                <div class="dfw-profile-grid dfw-profile-grid-stack">
                  <div>
                    <span>تقييم المصادقة</span>
                    <b>{{ pick(profileRow, 'authRating', 'AuthRating') ?? '—' }}</b>
                  </div>
                  <div class="dfw-profile-wide">
                    <span>ملاحظة التقييم</span>
                    <b>{{ pick(profileRow, 'authNote', 'AuthNote') || '—' }}</b>
                  </div>
                  <div>
                    <span>قيمة بضاعة المحل</span>
                    <b>{{ fmt(pick(profileRow, 'shopStockEstimatedValue', 'ShopStockEstimatedValue')) }}</b>
                  </div>
                  <div>
                    <span>قيمة الوارد اليومي</span>
                    <b>{{ fmt(pick(profileRow, 'estimatedDailyRevenue', 'EstimatedDailyRevenue')) }}</b>
                  </div>
                  <div>
                    <span>وقت المصادقة</span>
                    <b>{{ fmtDateTime(pick(profileRow, 'authenticatedAtUtc', 'AuthenticatedAtUtc')) }}</b>
                  </div>
                </div>
              </section>

              <section class="dfw-profile-box">
                <h3 class="dfw-profile-box-title">
                  تقييم المندوب
                </h3>
                <div class="dfw-profile-grid dfw-profile-grid-stack">
                  <div>
                    <span>تقييم المحل</span>
                    <b>{{ pick(profileRow, 'shopRating', 'ShopRating') ?? '—' }}</b>
                  </div>
                  <div class="dfw-profile-wide">
                    <span>ملاحظة التقييم</span>
                    <b>{{ pick(profileRow, 'shopRatingNote', 'ShopRatingNote') || '—' }}</b>
                  </div>
                  <div>
                    <span>قيمة بضاعة المحل</span>
                    <b>{{ fmt(pick(profileRow, 'delegateShopStockEstimatedValue', 'DelegateShopStockEstimatedValue')) }}</b>
                  </div>
                  <div>
                    <span>قيمة الوارد اليومي</span>
                    <b>{{ fmt(pick(profileRow, 'delegateEstimatedDailyRevenue', 'DelegateEstimatedDailyRevenue')) }}</b>
                  </div>
                </div>
              </section>

              <section class="dfw-profile-box">
                <h3 class="dfw-profile-box-title">
                  تقييم موظف المبيعات
                </h3>
                <div class="dfw-profile-grid dfw-profile-grid-stack">
                  <div>
                    <span>قيمة الموجودات</span>
                    <b>{{ fmt(pick(profileRow, 'employeeShopStockEstimatedValue', 'EmployeeShopStockEstimatedValue')) }}</b>
                  </div>
                  <div>
                    <span>الإيراد اليومي</span>
                    <b>{{ fmt(pick(profileRow, 'employeeEstimatedDailyRevenue', 'EmployeeEstimatedDailyRevenue')) }}</b>
                  </div>
                </div>
              </section>
            </div>
          </div>

          <div class="dfw-section-label mt-4">
            أسماء مصادقة المبيعات
            <span class="dfw-hint-inline">بانتظار {{ pendingSalesCount }} · مصادق {{ authenticatedSalesCount }}</span>
          </div>
          <div class="dfw-hint">
            قيد الانتظار = بيع موظف مبيعات منشور ولم تُصادق بعد على قوائم هذا المتابع
          </div>
          <div class="dfw-name-grid">
            <button
              v-for="(r, i) in salesRows"
              :key="`sale-${i}`"
              type="button"
              class="dfw-name-card"
              :class="{ 'dfw-name-card-active': Number(pick(profileRow, 'customerId', 'CustomerId')) === Number(pick(r, 'customerId', 'CustomerId')) }"
              @click="openCustomerProfile(r)"
            >
              <div class="dfw-name-card-title">
                {{ pick(r, 'customerName', 'CustomerName') }}
                <span
                  class="dfw-badge"
                  :class="salesAuthBadgeClass(r)"
                >
                  {{ (r.authenticated ?? r.Authenticated) ? 'مصادق' : 'بانتظار' }}
                </span>
              </div>
              <div class="dfw-name-card-sub">
                {{ pick(r, 'listName', 'ListName') || '—' }}
                · {{ pick(r, 'sellerName', 'SellerName') || '—' }}
                · {{ pick(r, 'currentRatingLabel', 'CurrentRatingLabel') || '—' }}
                · متبقي {{ fmt(pick(r, 'remainingAmount', 'RemainingAmount')) }}
              </div>
            </button>
            <div
              v-if="!salesRows.length"
              class="dfw-empty-block"
            >
              لا بيع موظف مبيعات بانتظار مصادقة، ولا مصادقات على قوائم هذا المتابع
            </div>
          </div>
        </template>

        <template v-else-if="detailTab === 'declining'">
          <div class="dfw-hint">
            مقارنة مع لقطة خلال آخر 7 أيام — هبوط درجتين (ممتاز→ضعيف، جيد→متلكئ، ضعيف→سيء) بنفس منطق تطبيق المتابع
          </div>
          <div class="dfw-name-grid">
            <button
              v-for="(r, i) in decliningRows"
              :key="i"
              type="button"
              class="dfw-name-card"
              @click="openCustomerProfile(r)"
            >
              <div class="dfw-name-card-title">
                {{ pick(r, 'customerName', 'CustomerName') }}
              </div>
              <div class="dfw-name-card-sub">
                {{ pick(r, 'fromLabel', 'FromLabel') || '—' }}
                → {{ pick(r, 'toLabel', 'ToLabel') || pick(r, 'ratingLabel', 'RatingLabel') || '—' }}
                · {{ pick(r, 'daysBetween', 'DaysBetween') ?? '—' }} يوم
                · {{ pick(r, 'listName', 'ListName') || '—' }}
              </div>
            </button>
            <div
              v-if="!decliningRows.length"
              class="dfw-empty-block"
            >
              لا زبائن متدنية حالياً
            </div>
          </div>
          <div
            v-if="profileRow"
            class="dfw-profile"
          >
            <div class="dfw-profile-head">
              <div class="dfw-profile-title">
                بروفايل الزبون — {{ pick(profileRow, 'customerName', 'CustomerName') }}
              </div>
              <VBtn
                size="small"
                variant="text"
                @click="closeProfile"
              >
                إغلاق البروفايل
              </VBtn>
            </div>
            <div class="dfw-profile-grid">
              <div><span>الهاتف</span><b>{{ pick(profileRow, 'phone', 'Phone') || '—' }}</b></div>
              <div><span>القائمة</span><b>{{ pick(profileRow, 'listName', 'ListName') || '—' }}</b></div>
              <div><span>من</span><b>{{ pick(profileRow, 'fromLabel', 'FromLabel') || '—' }}</b></div>
              <div><span>إلى</span><b>{{ pick(profileRow, 'toLabel', 'ToLabel') || pick(profileRow, 'ratingLabel', 'RatingLabel') || '—' }}</b></div>
              <div><span>الأيام</span><b>{{ pick(profileRow, 'daysBetween', 'DaysBetween') ?? '—' }}</b></div>
              <div><span>ملاحظة</span><b>{{ (profileRow.hasNote ?? profileRow.HasNote) ? 'نعم' : 'لا' }}</b></div>
            </div>
          </div>
        </template>

        <template v-else-if="detailTab === 'notes'">
          <div class="dfw-name-grid">
            <button
              v-for="(r, i) in noteRows"
              :key="i"
              type="button"
              class="dfw-name-card"
              @click="openCustomerProfile(r)"
            >
              <div class="dfw-name-card-title">
                {{ pick(r, 'customerName', 'CustomerName') }}
              </div>
              <div class="dfw-name-card-sub">
                {{ pick(r, 'listName', 'ListName') || '—' }}
                · {{ pick(r, 'authorName', 'AuthorName') || '—' }}
                · {{ fmtDateTime(pick(r, 'createdAtUtc', 'CreatedAtUtc')) }}
              </div>
              <div class="dfw-name-card-note">
                {{ pick(r, 'noteText', 'NoteText') || '—' }}
              </div>
            </button>
            <div
              v-if="!noteRows.length"
              class="dfw-empty-block"
            >
              لا ملاحظات تقييم
            </div>
          </div>
          <div
            v-if="profileRow"
            class="dfw-profile"
          >
            <div class="dfw-profile-head">
              <div class="dfw-profile-title">
                بروفايل الزبون — {{ pick(profileRow, 'customerName', 'CustomerName') }}
              </div>
              <VBtn
                size="small"
                variant="text"
                @click="closeProfile"
              >
                إغلاق البروفايل
              </VBtn>
            </div>
            <div class="dfw-profile-grid">
              <div><span>الهاتف</span><b>{{ pick(profileRow, 'phone', 'Phone') || '—' }}</b></div>
              <div><span>القائمة</span><b>{{ pick(profileRow, 'listName', 'ListName') || '—' }}</b></div>
              <div><span>الكاتب</span><b>{{ pick(profileRow, 'authorName', 'AuthorName') || '—' }}</b></div>
              <div><span>الوقت</span><b>{{ fmtDateTime(pick(profileRow, 'createdAtUtc', 'CreatedAtUtc')) }}</b></div>
              <div class="dfw-profile-wide"><span>الملاحظة</span><b>{{ pick(profileRow, 'noteText', 'NoteText') || '—' }}</b></div>
            </div>
          </div>
        </template>

        <template v-else-if="detailTab === 'oldCustomers'">
          <div class="dfw-hint">
            الزبائن القديمة (قانونية) — يرسلها مدير المبيعات إلى هذا المتابع
          </div>
          <div class="dfw-name-grid">
            <button
              v-for="(r, i) in oldCustomerRows"
              :key="pick(r, 'id', 'Id') || i"
              type="button"
              class="dfw-name-card"
              @click="openCustomerProfile(r)"
            >
              <div class="dfw-name-card-title">
                {{ pick(r, 'customerName', 'CustomerName') }}
                <span
                  class="dfw-badge"
                  :class="(r.isDone ?? r.IsDone) ? 'dfw-badge-on' : 'dfw-badge-warn'"
                >
                  {{ pick(r, 'statusLabel', 'StatusLabel') || ((r.isDone ?? r.IsDone) ? 'تمت المتابعة' : 'قيد العمل') }}
                </span>
              </div>
              <div class="dfw-name-card-sub">
                {{ pick(r, 'phone', 'Phone') || pick(r, 'address', 'Address') || '—' }}
                · أُرسل {{ fmtDateTime(pick(r, 'createdAtUtc', 'CreatedAtUtc')) }}
              </div>
            </button>
            <div
              v-if="!oldCustomerRows.length"
              class="dfw-empty-block"
            >
              لا زبائن قديمة لهذا المتابع
            </div>
          </div>
          <div
            v-if="profileRow"
            class="dfw-profile"
          >
            <div class="dfw-profile-head">
              <div class="dfw-profile-title">
                زبون قديم — {{ pick(profileRow, 'customerName', 'CustomerName') }}
              </div>
              <div class="d-flex align-center gap-2">
                <VBtn
                  v-if="canCancelOldCustomer(profileRow)"
                  size="small"
                  color="error"
                  variant="tonal"
                  @click="openCancelLegalDialog"
                >
                  إلغاء
                </VBtn>
                <VBtn
                  size="small"
                  variant="text"
                  @click="closeProfile"
                >
                  إغلاق البروفايل
                </VBtn>
              </div>
            </div>
            <div class="dfw-profile-grid">
              <div><span>الهاتف</span><b>{{ pick(profileRow, 'phone', 'Phone') || '—' }}</b></div>
              <div><span>العنوان</span><b>{{ pick(profileRow, 'address', 'Address') || '—' }}</b></div>
              <div><span>الحالة</span><b>{{ pick(profileRow, 'statusLabel', 'StatusLabel') || ((profileRow.isDone ?? profileRow.IsDone) ? 'تمت المتابعة' : 'قيد العمل') }}</b></div>
              <div><span>أُرسل</span><b>{{ fmtDateTime(pick(profileRow, 'createdAtUtc', 'CreatedAtUtc')) }}</b></div>
              <div><span>اكتمل</span><b>{{ fmtDateTime(pick(profileRow, 'completedAtUtc', 'CompletedAtUtc')) }}</b></div>
              <div class="dfw-profile-wide"><span>ملاحظة</span><b>{{ pick(profileRow, 'note', 'Note') || pick(profileRow, 'completionNote', 'CompletionNote') || '—' }}</b></div>
            </div>
          </div>
        </template>

        <template v-else-if="detailTab === 'delegateNotes'">
          <div class="dfw-name-grid">
            <div
              v-for="(r, i) in delegateNoteRows"
              :key="i"
              class="dfw-name-card dfw-name-card-static"
            >
              <div class="dfw-name-card-title">
                {{ pick(r, 'listName', 'ListName') || pick(r, 'delegateId', 'DelegateId') }}
              </div>
              <div class="dfw-name-card-sub">
                {{ pick(r, 'authorName', 'AuthorName') || '—' }}
                · {{ fmtDateTime(pick(r, 'createdAtUtc', 'CreatedAtUtc')) }}
              </div>
              <div class="dfw-name-card-note">
                {{ pick(r, 'noteText', 'NoteText') || '—' }}
              </div>
            </div>
            <div
              v-if="!delegateNoteRows.length"
              class="dfw-empty-block"
            >
              لا ملاحظات على المندوبين
            </div>
          </div>
        </template>

        <template v-else>
          <div class="dfw-section-label">
            قوائم المتابع — تفعيل/إيقاف الجباية
          </div>
          <div class="dfw-list-grid">
            <div
              v-for="(list, i) in collectionBundle.lists"
              :key="i"
              class="dfw-list-card"
            >
              <div class="dfw-list-card-name">
                {{ pick(list, 'listName', 'ListName') || pick(list, 'listId', 'ListId') }}
              </div>
              <div class="dfw-collection-row">
                <span>الجباية</span>
                <VSwitch
                  :model-value="!!(list.followerCollectionEnabled ?? list.FollowerCollectionEnabled)"
                  :loading="toggleBusyId === Number(pick(list, 'listId', 'ListId'))"
                  :disabled="toggleBusyId > 0"
                  color="success"
                  hide-details
                  density="compact"
                  @update:model-value="v => toggleCollection(list, v)"
                />
              </div>
              <div class="dfw-name-card-sub">
                تعيين {{ fmtDate(pick(list, 'assignedAtUtc', 'AssignedAtUtc')) }}
              </div>
              <div class="dfw-name-card-sub">
                متابع يوم التفعيل: {{ fmt(pick(list, 'lastFollowerPaymentAmount', 'LastFollowerPaymentAmount')) }}
                ({{ fmtDate(pick(list, 'lastFollowerPaymentAt', 'LastFollowerPaymentAt')) }})
              </div>
              <div class="dfw-name-card-sub">
                مندوب أمس: {{ fmt(pick(list, 'lastDelegatePaymentAmount', 'LastDelegatePaymentAmount')) }}
                ({{ fmtDate(pick(list, 'lastDelegatePaymentAt', 'LastDelegatePaymentAt')) }})
              </div>
            </div>
            <div
              v-if="!collectionBundle.lists.length"
              class="dfw-empty-block"
            >
              لا قوائم مسندة لهذا المتابع
            </div>
          </div>

          <div class="dfw-section-label mt-4">
            دفعات الجباية
          </div>
          <div class="dfw-name-grid">
            <button
              v-for="(p, i) in collectionBundle.payments"
              :key="i"
              type="button"
              class="dfw-name-card"
              @click="openCustomerProfile(p)"
            >
              <div class="dfw-name-card-title">
                {{ pick(p, 'customerName', 'CustomerName') }}
              </div>
              <div class="dfw-name-card-sub">
                {{ pick(p, 'listName', 'ListName') || '—' }}
                · {{ fmt(pick(p, 'amount', 'Amount')) }}
                · {{ fmtDate(pick(p, 'paymentDate', 'PaymentDate')) }}
                · {{ pick(p, 'source', 'Source') || '—' }}
              </div>
            </button>
            <div
              v-if="!collectionBundle.payments.length"
              class="dfw-empty-block"
            >
              لا دفعات جباية
            </div>
          </div>
          <div
            v-if="profileRow"
            class="dfw-profile"
          >
            <div class="dfw-profile-head">
              <div class="dfw-profile-title">
                بروفايل الزبون — {{ pick(profileRow, 'customerName', 'CustomerName') }}
              </div>
              <VBtn
                size="small"
                variant="text"
                @click="closeProfile"
              >
                إغلاق البروفايل
              </VBtn>
            </div>
            <div class="dfw-profile-grid">
              <div><span>القائمة</span><b>{{ pick(profileRow, 'listName', 'ListName') || '—' }}</b></div>
              <div><span>المبلغ</span><b>{{ fmt(pick(profileRow, 'amount', 'Amount')) }}</b></div>
              <div><span>التاريخ</span><b>{{ fmtDate(pick(profileRow, 'paymentDate', 'PaymentDate')) }}</b></div>
              <div><span>المصدر</span><b>{{ pick(profileRow, 'source', 'Source') || '—' }}</b></div>
            </div>
          </div>
        </template>
      </div>
    </div>

    <VDialog
      v-model="cancelLegalOpen"
      max-width="480"
      persistent
    >
      <VCard>
        <VCardTitle>
          إلغاء إرسال الزبون القديم
        </VCardTitle>
        <VCardText>
          <p class="mb-3 text-body-2">
            سيعود الطلب إلى <b>غير مسند</b> في لوحة المبيعات ويُزال من قائمة المتابع.
          </p>
          <VTextField
            v-model="cancelLegalReason"
            label="سبب الإلغاء *"
            density="comfortable"
            hide-details="auto"
            autofocus
          />
        </VCardText>
        <VCardActions>
          <VSpacer />
          <VBtn
            variant="text"
            :disabled="cancelLegalBusy"
            @click="cancelLegalOpen = false"
          >
            رجوع
          </VBtn>
          <VBtn
            color="error"
            :loading="cancelLegalBusy"
            @click="submitCancelLegal"
          >
            تأكيد الإلغاء
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>

    <VDialog
      v-model="manageDialog"
      max-width="560"
      persistent
    >
      <VCard>
        <VCardTitle>
          {{ manageEditId ? 'تعديل متابع' : 'إنشاء متابع' }}
        </VCardTitle>
        <VCardText>
          <div
            v-if="manageLoading"
            class="dfw-loading"
          >
            <VProgressCircular
              indeterminate
              color="primary"
            />
          </div>
          <div
            v-else
            class="d-flex flex-column gap-3"
          >
            <VTextField
              v-model="manageForm.userName"
              label="اسم المستخدم *"
              density="comfortable"
              hide-details="auto"
            />
            <VTextField
              v-model="manageForm.password"
              :label="manageEditId ? 'كلمة المرور (اتركها فارغة للإبقاء)' : 'كلمة المرور *'"
              type="password"
              density="comfortable"
              hide-details="auto"
            />
            <VTextField
              v-model="manageForm.phoneNumber"
              label="الهاتف"
              density="comfortable"
              hide-details="auto"
            />
            <VSelect
              v-model="manageForm.listIds"
              :items="manageLists"
              item-title="listName"
              item-value="listId"
              label="القوائم"
              multiple
              chips
              closable-chips
              density="comfortable"
              hide-details="auto"
            />
            <div class="text-medium-emphasis text-caption">
              المحافظة: {{ selected?.name || '—' }}
            </div>
          </div>
        </VCardText>
        <VCardActions>
          <VSpacer />
          <VBtn
            variant="text"
            :disabled="manageSaving"
            @click="manageDialog = false"
          >
            إلغاء
          </VBtn>
          <VBtn
            color="primary"
            :loading="manageSaving"
            @click="saveFollowerManage"
          >
            حفظ
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>
  </div>
</template>

<style scoped>
.dfw-page {
  --dfw-teal: #0f766e;
  --dfw-ink: #1f2937;
  --dfw-muted: #6b7280;
  --dfw-line: #e5e7eb;
}

.dfw-toolbar {
  display: flex;
  flex-wrap: wrap;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
  margin-block-end: 16px;
}

.dfw-title {
  display: flex;
  align-items: center;
  gap: 8px;
  color: var(--dfw-ink);
  font-size: 1.55rem;
  font-weight: 800;
}

.dfw-title-icon {
  color: var(--dfw-teal);
}

.dfw-tab-chip {
  display: inline-block;
  margin-inline-start: 10px;
  padding: 2px 10px;
  border-radius: 999px;
  background: #ccfbf1;
  color: #0f766e;
  font-size: 0.78rem;
  font-weight: 800;
  vertical-align: middle;
}

.dfw-subtitle {
  margin-block-start: 4px;
  color: var(--dfw-muted);
  font-size: 0.92rem;
}

.dfw-filters {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 10px;
}

.dfw-select {
  background: #fff;
}

.dfw-select-city {
  min-inline-size: 180px;
}

.dfw-select-search {
  min-inline-size: 160px;
}

.dfw-refresh {
  background: linear-gradient(135deg, #0f766e, #14b8a6) !important;
  color: #fff !important;
  font-weight: 700;
  min-block-size: 44px;
  padding-inline: 18px;
  border-radius: 12px;
}

.dfw-stats {
  display: flex;
  gap: 12px;
  margin-block-end: 14px;
}

.dfw-stat {
  background: #fff;
  border: 1px solid var(--dfw-line);
  border-radius: 12px;
  padding: 10px 16px;
  min-inline-size: 120px;
}

.dfw-stat-label {
  display: block;
  color: var(--dfw-muted);
  font-size: 0.78rem;
  font-weight: 600;
}

.dfw-stat-value {
  font-size: 1.25rem;
  font-weight: 800;
  color: var(--dfw-ink);
}

.dfw-loading {
  display: flex;
  justify-content: center;
  padding: 48px;
}

.dfw-loading-sm {
  padding: 20px;
}

.dfw-follower-grid,
.dfw-list-grid,
.dfw-bucket-grid,
.dfw-name-grid {
  display: grid;
  gap: 12px;
}

.dfw-follower-grid {
  grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
}

.dfw-list-grid {
  grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
}

.dfw-bucket-grid {
  grid-template-columns: repeat(auto-fill, minmax(180px, 1fr));
  margin-block-end: 14px;
}

.dfw-name-grid {
  grid-template-columns: repeat(auto-fill, minmax(240px, 1fr));
}

.dfw-follower-card,
.dfw-list-card,
.dfw-bucket-card,
.dfw-name-card {
  text-align: start;
  background: #fff;
  border: 1px solid var(--dfw-line);
  border-radius: 14px;
  padding: 14px 16px;
  box-shadow: 0 4px 14px rgba(15, 23, 42, 0.04);
  cursor: pointer;
  transition: border-color 0.15s, box-shadow 0.15s, background 0.15s;
}

.dfw-follower-card:hover,
.dfw-bucket-card:hover,
.dfw-name-card:hover {
  border-color: #99f6e4;
  box-shadow: 0 6px 18px rgba(15, 118, 110, 0.1);
}

.dfw-follower-card-active,
.dfw-bucket-card-active,
.dfw-name-card-active {
  border-color: var(--dfw-teal);
  background: #ecfdf5;
}

.dfw-list-card-static,
.dfw-name-card-static {
  cursor: default;
}

.dfw-follower-card-name,
.dfw-list-card-name {
  font-weight: 800;
  color: var(--dfw-ink);
  font-size: 1.05rem;
  margin-block-end: 10px;
}

.dfw-follower-card-name {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
}

.dfw-card-actions {
  display: inline-flex;
  align-items: center;
  flex-shrink: 0;
}

.dfw-follower-card-meta {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 8px;
}

.dfw-meta-block span {
  display: block;
  color: var(--dfw-muted);
  font-size: 0.72rem;
  font-weight: 600;
}

.dfw-meta-block b {
  color: var(--dfw-teal);
  font-size: 1rem;
  font-weight: 800;
}

.dfw-rating-with-trend {
  display: inline-flex;
  align-items: center;
  gap: 4px;
}

.dfw-trend-up {
  color: #15803d !important;
}

.dfw-trend-down {
  color: #b91c1c !important;
}

.dfw-trend-flat {
  color: #64748b !important;
}

.dfw-bucket-card {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.dfw-bucket-card span {
  color: var(--dfw-muted);
  font-size: 0.82rem;
  font-weight: 700;
}

.dfw-bucket-card b {
  color: var(--dfw-ink);
  font-size: 1.35rem;
  font-weight: 800;
}

.dfw-name-card-title {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  font-weight: 800;
  color: var(--dfw-ink);
  margin-block-end: 4px;
}

.dfw-name-card-sub {
  color: var(--dfw-muted);
  font-size: 0.8rem;
  line-height: 1.45;
}

.dfw-name-card-note {
  margin-block-start: 8px;
  color: var(--dfw-ink);
  font-size: 0.88rem;
  white-space: pre-wrap;
  word-break: break-word;
}

.dfw-collection-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  margin-block-end: 8px;
  font-weight: 700;
}

.dfw-empty-block {
  grid-column: 1 / -1;
  color: var(--dfw-muted);
  padding: 28px;
  text-align: center;
  background: #fff;
  border: 1px dashed var(--dfw-line);
  border-radius: 14px;
}

.dfw-badge {
  display: inline-block;
  padding: 2px 10px;
  border-radius: 999px;
  font-size: 0.72rem;
  font-weight: 700;
  flex-shrink: 0;
}

.dfw-badge-on {
  background: #d1fae5;
  color: #047857;
}

.dfw-badge-warn {
  background: #fef3c7;
  color: #b45309;
}

.dfw-badge-danger {
  background: #fee2e2;
  color: #b91c1c;
}

.dfw-detail {
  margin-block-start: 18px;
  background: #fff;
  border: 1px solid var(--dfw-line);
  border-radius: 16px;
  box-shadow: 0 4px 18px rgba(15, 23, 42, 0.05);
  overflow: hidden;
}

.dfw-detail-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 14px 18px;
  border-block-end: 1px solid var(--dfw-line);
  background: #f0faf8;
}

.dfw-detail-title {
  font-weight: 800;
  font-size: 1.15rem;
  color: var(--dfw-ink);
}

.dfw-detail-sub {
  color: var(--dfw-muted);
  font-size: 0.82rem;
  margin-block-start: 2px;
}

.dfw-detail-body {
  padding: 14px 16px 20px;
}

.dfw-section-label {
  font-weight: 800;
  color: var(--dfw-ink);
  margin-block-end: 8px;
}

.dfw-hint {
  color: var(--dfw-muted);
  font-size: 0.85rem;
  margin-block-end: 10px;
}

.dfw-hint-inline {
  margin-inline-start: 10px;
  color: var(--dfw-muted);
  font-size: 0.82rem;
  font-weight: 600;
}

.dfw-panel {
  margin-block-end: 14px;
  padding: 12px;
  border: 1px solid var(--dfw-line);
  border-radius: 14px;
  background: #f8fafc;
}

.dfw-profile {
  margin-block: 14px;
  padding: 14px;
  border: 1px solid #99f6e4;
  border-radius: 14px;
  background: linear-gradient(180deg, #f0fdfa, #fff);
}

.dfw-profile-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  margin-block-end: 10px;
}

.dfw-profile-title {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 8px;
  font-weight: 800;
  color: var(--dfw-teal);
}

.dfw-full-profile-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  margin: 0;
  padding: 6px 12px;
  border: 1px solid #0f766e;
  border-radius: 6px;
  background: #fff;
  color: #0f766e;
  font: inherit;
  font-size: 0.82rem;
  font-weight: 700;
  line-height: 1.3;
  cursor: pointer;
  white-space: nowrap;
}

.dfw-full-profile-btn:hover {
  background: #f0fdfa;
}

.dfw-profile-containers {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 12px;
  align-items: start;
}

@media (max-width: 1100px) {
  .dfw-profile-containers {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 640px) {
  .dfw-profile-containers {
    grid-template-columns: 1fr;
  }
}

.dfw-profile-box {
  background: #fff;
  border: 1px solid var(--dfw-line);
  border-radius: 12px;
  padding: 10px;
  min-height: 100%;
}

.dfw-profile-box-title {
  margin: 0 0 10px;
  font-size: 0.88rem;
  font-weight: 800;
  color: var(--dfw-ink);
}

.dfw-profile-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(180px, 1fr));
  gap: 10px;
}

.dfw-profile-grid-stack {
  grid-template-columns: 1fr;
}

.dfw-profile-grid div {
  background: #fff;
  border: 1px solid var(--dfw-line);
  border-radius: 10px;
  padding: 8px 10px;
}

.dfw-profile-wide {
  grid-column: 1 / -1;
}

.dfw-profile-grid span {
  display: block;
  color: var(--dfw-muted);
  font-size: 0.72rem;
  font-weight: 600;
}

.dfw-profile-grid b {
  font-size: 0.92rem;
  color: var(--dfw-ink);
  font-weight: 800;
  word-break: break-word;
}

.mt-4 {
  margin-block-start: 1rem;
}
</style>
