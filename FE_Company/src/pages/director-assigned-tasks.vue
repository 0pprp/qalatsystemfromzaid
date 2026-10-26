<script setup>
import { computed, onMounted, ref } from 'vue'
import {
  parseUtcMillis,
  smErrorMessage,
  smGet,
  smGetBlob,
  smGetFollowers,
  smPost,
  smPostForm,
  smPut,
} from '@/composables/salesManagerApi'
import { formatIraqClock, formatIraqDate } from '@/composables/iraqDate'
import { useToast } from '@/composables/useToast'

const toast = useToast()
const loading = ref(false)
const saving = ref(false)
const uploading = ref(false)
const rows = ref([])
const statusFilter = ref('الكل')
const createDialog = ref(false)
const detailDialog = ref(false)
const isEdit = ref(false)
const form = ref(emptyForm())
const pendingFiles = ref([])
const assigneeOptions = ref([])
const detail = ref(null)
const detailLoading = ref(false)

const STATUS_ALL = 'الكل'
const STATUS_OVERDUE = '__overdue__'
const STATUS_FILTER_ITEMS = [
  { title: 'الكل', value: STATUS_ALL },
  { title: 'متجاوزة المدة المحددة', value: STATUS_OVERDUE },
  { title: 'مرسلة', value: 'مرسلة' },
  { title: 'منقولة', value: 'منقولة' },
  { title: 'مقبولة', value: 'مقبولة' },
  { title: 'معادة', value: 'معادة' },
  { title: 'بانتظار تصديق المفوض', value: 'بانتظار_تصديق_المفوض' },
  { title: 'منجزة', value: 'منجزة' },
]

const PRIORITY_ITEMS = [
  { title: 'عالية', value: 1 },
  { title: 'متوسطة', value: 2 },
  { title: 'ضعيفة', value: 3 },
]

const HOUR_ITEMS = Array.from({ length: 12 }, (_, i) => ({
  title: String(i + 1),
  value: i + 1,
}))

const MINUTE_ITEMS = Array.from({ length: 60 }, (_, i) => ({
  title: String(i).padStart(2, '0'),
  value: i,
}))

const PERIOD_ITEMS = [
  { title: 'صباح', value: 'صباح' },
  { title: 'مساء', value: 'مساء' },
]

function emptyForm() {
  return {
    id: null,
    title: '',
    description: '',
    dueDate: '',
    dueHour: 12,
    dueMinute: 0,
    duePeriod: 'صباح',
    priority: 2,
    assigneeOptionKey: null,
    assigneeCityValue: null,
  }
}

function pick(obj, ...keys) {
  if (!obj)
    return undefined
  for (const key of keys) {
    if (obj[key] != null && obj[key] !== '')
      return obj[key]
  }

  return undefined
}

function priorityLabel(value) {
  const n = Number(value)
  if (n === 1)
    return 'عالية'
  if (n === 3)
    return 'ضعيفة'

  return 'متوسطة'
}

function priorityColor(value) {
  const n = Number(value)
  if (n === 1)
    return 'error'
  if (n === 3)
    return 'secondary'

  return 'warning'
}

function statusLabel(status) {
  const s = String(status || '')
  if (s === 'بانتظار_تصديق_المفوض')
    return 'بانتظار تصديق المفوض'
  if (s === 'منقولة')
    return 'منقولة'

  return s || '—'
}

function statusColor(status) {
  const s = String(status || '')
  if (s === 'منجزة')
    return 'success'
  if (s === 'معادة')
    return 'error'
  if (s === 'مقبولة')
    return 'primary'
  if (s === 'منقولة')
    return 'secondary'
  if (s === 'بانتظار_تصديق_المفوض')
    return 'warning'

  return 'info'
}

function dueDateText(value) {
  if (value == null || value === '')
    return '—'
  const text = String(value).trim()
  const m = text.match(/^(\d{4})[-/](\d{2})[-/](\d{2})/)
  if (m)
    return `${m[1]}/${m[2]}/${m[3]}`

  return formatIraqDate(value) || text
}

function dueDisplay(row) {
  const dueAt = pick(row, 'dueAtUtc', 'DueAtUtc')
  const day = dueDateText(pick(row, 'dueDate', 'DueDate'))
  if (dueAt) {
    const clock = iraqTimePart(dueAt)
    return clock && clock !== '—' ? `${day} · ${clock}` : day
  }

  return day
}

function stampDisplay(iso) {
  if (!iso)
    return '—'
  const d = iraqDatePart(iso)
  const t = iraqTimePart(iso)
  if (d === '—' && t === '—')
    return '—'
  if (t === '—')
    return d

  return `${d} · ${t}`
}

function duePartsFromUtc(iso) {
  if (!iso)
    return { hour: 12, minute: 0, period: 'صباح' }
  const ms = parseUtcMillis(iso)
  if (!Number.isFinite(ms))
    return { hour: 12, minute: 0, period: 'صباح' }
  const parts = new Intl.DateTimeFormat('en-US', {
    timeZone: 'Asia/Baghdad',
    hour: 'numeric',
    minute: '2-digit',
    hour12: true,
  }).formatToParts(new Date(ms))
  const pickPart = type => parts.find(p => p.type === type)?.value || ''
  let hour = Number(pickPart('hour')) || 12
  if (hour < 1 || hour > 12)
    hour = 12
  const minute = Number(pickPart('minute')) || 0
  const period = /p/i.test(pickPart('dayPeriod')) ? 'مساء' : 'صباح'

  return { hour, minute, period }
}

function dueDateInputValue(value) {
  if (value == null || value === '')
    return ''
  const text = String(value).trim()
  const m = text.match(/^(\d{4})[-/](\d{2})[-/](\d{2})/)
  if (m)
    return `${m[1]}-${m[2]}-${m[3]}`

  return text.slice(0, 10)
}

function iraqDatePart(iso) {
  return formatIraqDate(iso) || '—'
}

function iraqTimePart(iso) {
  return formatIraqClock(iso) || '—'
}

function optionValue(opt) {
  return `${opt.kind}::${opt.key}`
}

function parseOptionKey(value) {
  const raw = String(value || '')
  const idx = raw.indexOf('::')
  if (idx < 0)
    return null
  return {
    kind: raw.slice(0, idx),
    key: raw.slice(idx + 2),
  }
}

function buildAssigneeItems(payload, followerRows = []) {
  const dm = Array.isArray(payload?.delegatedManagers) ? payload.delegatedManagers : []
  const bm = Array.isArray(payload?.branchManagers) ? payload.branchManagers : []
  const fcFromApi = Array.isArray(payload?.fieldCommitteeFollowers)
    ? payload.fieldCommitteeFollowers
    : []
  const items = []
  const seen = new Set()
  const pushUnique = (item) => {
    if (!item?.value || seen.has(item.value))
      return
    seen.add(item.value)
    items.push(item)
  }

  for (const row of dm) {
    const kind = pick(row, 'kind', 'Kind') || 'DelegatedManager'
    const key = String(pick(row, 'key', 'Key') || '')
    if (!key)
      continue
    const name = pick(row, 'displayName', 'DisplayName') || pick(row, 'userName', 'UserName') || key
    pushUnique({
      title: `${name} (المدير المفوض)`,
      value: optionValue({ kind, key }),
      kind,
      key,
      displayName: name,
      cityValue: '',
    })
  }
  for (const row of bm) {
    const kind = pick(row, 'kind', 'Kind') || 'BranchManager'
    const key = String(pick(row, 'key', 'Key') || '')
    if (!key)
      continue
    const name = pick(row, 'displayName', 'DisplayName') || pick(row, 'userName', 'UserName') || key
    const cityObjs = Array.isArray(pick(row, 'cities', 'Cities')) ? pick(row, 'cities', 'Cities') : []
    const cityValuesRaw = pick(row, 'cityValues', 'CityValues')
    const cities = []
    const seenCity = new Set()
    for (const c of cityObjs) {
      const cv = String(pick(c, 'cityValue', 'CityValue') || '').trim()
      if (!cv || seenCity.has(cv))
        continue
      seenCity.add(cv)
      cities.push({
        value: cv,
        title: String(pick(c, 'cityName', 'CityName') || cv).trim() || cv,
      })
    }
    if (Array.isArray(cityValuesRaw)) {
      for (const raw of cityValuesRaw) {
        const cv = String(raw || '').trim()
        if (!cv || seenCity.has(cv))
          continue
        seenCity.add(cv)
        cities.push({ value: cv, title: cv })
      }
    }
    const cityValue = cities.length === 1 ? cities[0].value : ''
    pushUnique({
      title: cities.length > 1
        ? `${name} (إدارة الفرع — ${cities.length} محافظات)`
        : `${name} (إدارة الفرع)`,
      value: optionValue({ kind, key }),
      kind,
      key,
      displayName: name,
      cityValue,
      cities,
    })
  }

  const followerSource = fcFromApi.length ? fcFromApi : followerRows
  for (const row of followerSource) {
    const kind = pick(row, 'kind', 'Kind') || 'FieldCommitteeFollower'
    const cityValue = String(pick(row, 'cityValue', 'CityValue') || '')
    const followerId = pick(row, 'key', 'Key', 'followerId', 'FollowerId', 'userId', 'UserId')
    if (followerId == null || followerId === '')
      continue
    const idNum = Number(followerId)
    if (!Number.isFinite(idNum) || idNum <= 0)
      continue
    const key = cityValue
      ? `${cityValue}|${idNum}`
      : String(pick(row, 'key', 'Key') || idNum)
    const name = pick(row, 'displayName', 'DisplayName', 'followerName', 'FollowerName')
      || pick(row, 'userName', 'UserName')
      || key
    const cityLabel = pick(row, 'cityName', 'CityName') || cityValue
    pushUnique({
      title: cityLabel ? `${name} (مسؤول اللجنة — ${cityLabel})` : `${name} (مسؤول اللجنة)`,
      value: optionValue({ kind, key }),
      kind,
      key,
      displayName: name,
      cityValue,
    })
  }

  return items
}

function iraqTodayYmd() {
  return new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Asia/Baghdad',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).format(new Date())
}

function isTaskOverdue(row) {
  const status = String(pick(row, 'status', 'Status') || '')
  // أي حالة غير منجزة تخضع لتاريخ/وقت الاستحقاق بتوقيت بغداد.
  if (status === 'منجزة')
    return false

  const dueAt = pick(row, 'dueAtUtc', 'DueAtUtc')
  if (dueAt) {
    // DueAtUtc دائماً UTC — لا تستخدم Date.parse مباشرة (بدون Z يُفسَّر كوقت الجهاز).
    const ms = parseUtcMillis(dueAt)
    if (Number.isFinite(ms))
      return ms < Date.now()
  }

  const dueRaw = pick(row, 'dueDate', 'DueDate')
  if (dueRaw != null && dueRaw !== '') {
    const text = String(dueRaw).trim()
    const m = text.match(/^(\d{4})[-/](\d{2})[-/](\d{2})/)
    if (m) {
      const dueDay = `${m[1]}-${m[2]}-${m[3]}`
      return dueDay < iraqTodayYmd()
    }
  }

  const flag = pick(row, 'isOverdue', 'IsOverdue')
  return flag === true || flag === 1 || String(flag).toLowerCase() === 'true'
}

const filteredRows = computed(() => {
  if (statusFilter.value === STATUS_ALL)
    return rows.value
  if (statusFilter.value === STATUS_OVERDUE)
    return rows.value.filter(r => isTaskOverdue(r))

  return rows.value.filter(r => String(pick(r, 'status', 'Status') || '') === statusFilter.value)
})

async function loadAssignees() {
  try {
    const [data, followers] = await Promise.all([
      smGet('assigned-tasks/assignee-options'),
      smGetFollowers('').catch(() => []),
    ])
    assigneeOptions.value = buildAssigneeItems(data, Array.isArray(followers) ? followers : [])
  }
  catch (err) {
    assigneeOptions.value = []
    toast.error(smErrorMessage(err, 'تعذر تحميل خيارات الإسناد'))
  }
}

async function load() {
  loading.value = true
  try {
    const list = await smGet('assigned-tasks')
    rows.value = Array.isArray(list) ? list : []
  }
  catch (err) {
    rows.value = []
    toast.error(smErrorMessage(err, 'تعذر تحميل المهام'))
  }
  finally {
    loading.value = false
  }
}

function openCreate() {
  isEdit.value = false
  form.value = emptyForm()
  pendingFiles.value = []
  createDialog.value = true
}

const selectedAssignee = computed(() =>
  assigneeOptions.value.find(o => o.value === form.value.assigneeOptionKey) || null)

const showCityPicker = computed(() => {
  const opt = selectedAssignee.value
  if (!opt)
    return false
  if (opt.kind === 'BranchManager')
    return Array.isArray(opt.cities) && opt.cities.length > 0
  return false
})

const cityPickerItems = computed(() => {
  const opt = selectedAssignee.value
  if (!opt || !Array.isArray(opt.cities))
    return []
  return opt.cities
})

function onAssigneeChange() {
  const opt = selectedAssignee.value
  if (!opt) {
    form.value.assigneeCityValue = null
    return
  }
  if (opt.kind === 'BranchManager' && Array.isArray(opt.cities) && opt.cities.length === 1)
    form.value.assigneeCityValue = opt.cities[0].value
  else if (opt.kind === 'BranchManager')
    form.value.assigneeCityValue = null
  else
    form.value.assigneeCityValue = opt.cityValue || null
}

function openEdit(row) {
  const status = pick(row, 'status', 'Status')
  if (status !== 'معادة')
    return
  isEdit.value = true
  const kind = pick(row, 'assigneeKind', 'AssigneeKind') || ''
  const key = String(pick(row, 'assigneeKey', 'AssigneeKey') || '')
  const dueParts = duePartsFromUtc(pick(row, 'dueAtUtc', 'DueAtUtc'))
  form.value = {
    id: pick(row, 'id', 'Id'),
    title: pick(row, 'title', 'Title') || '',
    description: pick(row, 'description', 'Description') || '',
    dueDate: dueDateInputValue(pick(row, 'dueDate', 'DueDate')),
    dueHour: dueParts.hour,
    dueMinute: dueParts.minute,
    duePeriod: dueParts.period,
    priority: Number(pick(row, 'priority', 'Priority') ?? 2) || 2,
    assigneeOptionKey: kind && key ? optionValue({ kind, key }) : null,
    assigneeCityValue: pick(row, 'assigneeCityValue', 'AssigneeCityValue') || null,
  }
  pendingFiles.value = []
  createDialog.value = true
}

function onFilesChange(files) {
  const list = Array.isArray(files) ? files : (files ? [files] : [])
  pendingFiles.value = list.filter(Boolean)
}

async function uploadFiles(taskId, files) {
  const list = (files || []).filter(Boolean)
  if (!list.length || !taskId)
    return
  uploading.value = true
  try {
    for (const file of list) {
      const fd = new FormData()
      fd.append('file', file, file.name || 'attachment')
      await smPostForm(`assigned-tasks/${taskId}/attachments`, fd)
    }
  }
  finally {
    uploading.value = false
  }
}

async function save() {
  const title = String(form.value.title || '').trim()
  if (!title) {
    toast.error('أدخل عنوان المهمة')

    return
  }
  if (!form.value.dueDate) {
    toast.error('اختر تاريخ الاستحقاق')

    return
  }
  const parsed = parseOptionKey(form.value.assigneeOptionKey)
  const opt = assigneeOptions.value.find(o => o.value === form.value.assigneeOptionKey)
  if (!parsed || !opt) {
    toast.error('اختر المسند إليه')

    return
  }

  let cityValue = form.value.assigneeCityValue || opt.cityValue || null
  if (opt.kind === 'BranchManager') {
    if (Array.isArray(opt.cities) && opt.cities.length > 0) {
      cityValue = form.value.assigneeCityValue || (opt.cities.length === 1 ? opt.cities[0].value : null)
      if (!cityValue) {
        toast.error('اختر المحافظة لهذه المهمة')

        return
      }
    }
  }

  const payload = {
    title,
    description: String(form.value.description || '').trim(),
    dueDate: dueDateInputValue(form.value.dueDate),
    dueHour: Number(form.value.dueHour) || 12,
    dueMinute: Number(form.value.dueMinute) || 0,
    duePeriod: form.value.duePeriod === 'مساء' ? 'مساء' : 'صباح',
    priority: Number(form.value.priority) || 2,
    assigneeKind: parsed.kind,
    assigneeKey: parsed.key,
    assigneeDisplayName: opt.displayName,
    assigneeCityValue: cityValue,
  }

  saving.value = true
  try {
    let taskId = form.value.id
    if (isEdit.value) {
      await smPut(`assigned-tasks/${taskId}`, payload)
      toast.success('تم تحديث المهمة وإعادة إرسالها')
    }
    else {
      const created = await smPost('assigned-tasks', payload)
      taskId = pick(created, 'id', 'Id')
      toast.success('تم إنشاء المهمة')
    }

    if (pendingFiles.value.length && taskId) {
      try {
        await uploadFiles(taskId, pendingFiles.value)
        toast.success('تم رفع المرفقات')
      }
      catch (err) {
        toast.error(smErrorMessage(err, 'تم حفظ المهمة لكن تعذر رفع بعض المرفقات'))
      }
    }

    createDialog.value = false
    await load()
    if (detailDialog.value && detail.value) {
      const openId = pick(detail.value.task, 'id', 'Id')
      if (openId && String(openId) === String(taskId))
        await openDetail({ id: taskId })
    }
  }
  catch (err) {
    toast.error(smErrorMessage(err, isEdit.value ? 'تعذر تحديث المهمة' : 'تعذر إنشاء المهمة'))
  }
  finally {
    saving.value = false
  }
}

async function openDetail(row) {
  const id = pick(row, 'id', 'Id')
  if (!id)
    return
  detailDialog.value = true
  detailLoading.value = true
  detail.value = null
  try {
    const data = await smGet(`assigned-tasks/${id}`)
    detail.value = {
      task: pick(data, 'task', 'Task') || data,
      events: Array.isArray(pick(data, 'events', 'Events')) ? pick(data, 'events', 'Events') : [],
      attachments: Array.isArray(pick(data, 'attachments', 'Attachments')) ? pick(data, 'attachments', 'Attachments') : [],
    }
  }
  catch (err) {
    detailDialog.value = false
    toast.error(smErrorMessage(err, 'تعذر تحميل تفاصيل المهمة'))
  }
  finally {
    detailLoading.value = false
  }
}

async function downloadAttachment(file) {
  const taskId = pick(detail.value?.task, 'id', 'Id')
  const fileId = pick(file, 'id', 'Id')
  if (!taskId || !fileId)
    return
  try {
    const blob = await smGetBlob(`assigned-tasks/${taskId}/attachments/${fileId}`)
    const name = pick(file, 'fileName', 'FileName') || 'attachment'
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = name
    a.click()
    URL.revokeObjectURL(url)
  }
  catch (err) {
    toast.error(smErrorMessage(err, 'تعذر تنزيل المرفق'))
  }
}

async function uploadMoreInDetail(files) {
  const taskId = pick(detail.value?.task, 'id', 'Id')
  const list = Array.isArray(files) ? files : (files ? [files] : [])
  if (!taskId || !list.length)
    return
  try {
    await uploadFiles(taskId, list)
    toast.success('تم رفع المرفق')
    await openDetail({ id: taskId })
  }
  catch (err) {
    toast.error(smErrorMessage(err, 'تعذر رفع المرفق'))
  }
}

const detailTask = computed(() => detail.value?.task || null)
const canEditDetail = computed(() => pick(detailTask.value, 'status', 'Status') === 'معادة')

const timelineHeaders = [
  { title: 'تاريخ', key: 'date' },
  { title: 'وقت', key: 'time' },
  { title: 'الناقل', key: 'actor' },
  { title: 'دور الناقل', key: 'actorRole' },
  { title: 'المنقول', key: 'target' },
  { title: 'دور المنقول', key: 'targetRole' },
  { title: 'ملاحظة', key: 'note' },
]

const timelineRows = computed(() => {
  const events = detail.value?.events || []

  return events.map(e => ({
    id: pick(e, 'id', 'Id'),
    date: iraqDatePart(pick(e, 'atUtc', 'AtUtc')),
    time: iraqTimePart(pick(e, 'atUtc', 'AtUtc')),
    actor: pick(e, 'actorDisplayName', 'ActorDisplayName') || '—',
    actorRole: pick(e, 'actorRoleLabel', 'ActorRoleLabel') || '—',
    target: pick(e, 'targetDisplayName', 'TargetDisplayName') || '—',
    targetRole: pick(e, 'targetRoleLabel', 'TargetRoleLabel') || '—',
    note: pick(e, 'note', 'Note') || '—',
    eventType: pick(e, 'eventType', 'EventType') || '',
  }))
})

onMounted(async () => {
  await Promise.all([load(), loadAssignees()])
})
</script>

<template>
  <div>
    <div class="d-flex flex-wrap align-center justify-space-between gap-3 mb-4">
      <div>
        <h4 class="mb-1">
          المهام
        </h4>
        <p class="text-medium-emphasis mb-0">
          مهام شخصية تُسند للمفوض أو إدارة الفرع — منفصلة عن مهام اللجنة.
        </p>
      </div>
      <div class="d-flex flex-wrap align-center gap-2">
        <VSelect
          v-model="statusFilter"
          :items="STATUS_FILTER_ITEMS"
          item-title="title"
          item-value="value"
          label="الحالة"
          density="compact"
          hide-details
          style="min-width: 200px"
        />
        <VBtn
          variant="tonal"
          color="primary"
          :loading="loading"
          @click="load"
        >
          تحديث
        </VBtn>
        <VBtn
          color="success"
          prepend-icon="tabler-plus"
          @click="openCreate"
        >
          مهمة جديدة
        </VBtn>
      </div>
    </div>

    <VCard :loading="loading">
      <VCardText v-if="!filteredRows.length && !loading">
        <div class="text-medium-emphasis text-center py-8">
          لا توجد مهام{{ statusFilter !== STATUS_ALL ? ' بهذه الحالة' : '' }}.
        </div>
      </VCardText>

      <div
        v-else
        class="d-flex flex-column gap-3 pa-4"
      >
        <VCard
          v-for="item in filteredRows"
          :key="pick(item, 'id', 'Id')"
          variant="outlined"
          class="pa-3"
          :class="{ 'assigned-task-overdue': isTaskOverdue(item) }"
        >
          <div class="d-flex flex-wrap align-start justify-space-between gap-3">
            <div class="flex-grow-1">
              <div class="d-flex flex-wrap align-center gap-2 mb-2">
                <strong class="text-h6">{{ pick(item, 'title', 'Title') || '—' }}</strong>
                <VChip
                  size="small"
                  :color="priorityColor(pick(item, 'priority', 'Priority'))"
                  variant="tonal"
                >
                  {{ priorityLabel(pick(item, 'priority', 'Priority')) }}
                </VChip>
                <VChip
                  size="small"
                  :color="statusColor(pick(item, 'status', 'Status'))"
                  variant="tonal"
                >
                  {{ statusLabel(pick(item, 'status', 'Status')) }}
                </VChip>
                <VChip
                  v-if="isTaskOverdue(item)"
                  size="small"
                  color="error"
                >
                  تجاوزت المدة
                </VChip>
              </div>
              <div class="text-body-2 text-medium-emphasis">
                الاستحقاق: {{ dueDisplay(item) }}
                · المسند: {{ pick(item, 'assigneeDisplayName', 'AssigneeDisplayName') || '—' }}
                <template v-if="pick(item, 'assigneeCityValue', 'AssigneeCityValue')">
                  · محافظة: {{ pick(item, 'assigneeCityValue', 'AssigneeCityValue') }}
                </template>
              </div>
              <div
                v-if="pick(item, 'transferredAtUtc', 'TransferredAtUtc')
                  || pick(item, 'branchCompletedAtUtc', 'BranchCompletedAtUtc')
                  || pick(item, 'delegatedConfirmedAtUtc', 'DelegatedConfirmedAtUtc')"
                class="text-caption text-medium-emphasis mt-1"
              >
                <span v-if="pick(item, 'transferredAtUtc', 'TransferredAtUtc')">
                  نقل: {{ stampDisplay(pick(item, 'transferredAtUtc', 'TransferredAtUtc')) }}
                </span>
                <span
                  v-if="pick(item, 'branchCompletedAtUtc', 'BranchCompletedAtUtc')"
                  class="ms-2"
                >
                  إنجاز الفرع: {{ stampDisplay(pick(item, 'branchCompletedAtUtc', 'BranchCompletedAtUtc')) }}
                </span>
                <span
                  v-if="pick(item, 'delegatedConfirmedAtUtc', 'DelegatedConfirmedAtUtc')"
                  class="ms-2"
                >
                  تصديق: {{ stampDisplay(pick(item, 'delegatedConfirmedAtUtc', 'DelegatedConfirmedAtUtc')) }}
                </span>
              </div>
              <div
                v-if="pick(item, 'description', 'Description')"
                class="text-body-2 mt-1"
              >
                {{ String(pick(item, 'description', 'Description')).length > 120
                  ? `${String(pick(item, 'description', 'Description')).slice(0, 120)}…`
                  : pick(item, 'description', 'Description') }}
              </div>
              <div
                v-if="pick(item, 'rejectNote', 'RejectNote')"
                class="text-body-2 text-error mt-1"
              >
                ملاحظة الرفض: {{ pick(item, 'rejectNote', 'RejectNote') }}
              </div>
            </div>
            <div class="d-flex align-center gap-1">
              <VBtn
                v-if="pick(item, 'status', 'Status') === 'معادة'"
                variant="tonal"
                color="warning"
                size="small"
                @click="openEdit(item)"
              >
                تعديل وإعادة إرسال
              </VBtn>
              <VBtn
                variant="tonal"
                color="primary"
                size="small"
                @click="openDetail(item)"
              >
                التفاصيل
              </VBtn>
            </div>
          </div>
        </VCard>
      </div>
    </VCard>

    <!-- Create / edit (معادة → re-send) -->
    <VDialog
      v-model="createDialog"
      max-width="640"
      persistent
    >
      <VCard>
        <VCardTitle class="d-flex align-center justify-space-between">
          <span>{{ isEdit ? 'تعديل وإعادة إرسال' : 'مهمة جديدة' }}</span>
          <VBtn
            icon
            variant="text"
            @click="createDialog = false"
          >
            <VIcon icon="tabler-x" />
          </VBtn>
        </VCardTitle>
        <VCardText class="d-flex flex-column gap-4">
          <VTextField
            v-model="form.title"
            label="العنوان"
          />
          <VTextarea
            v-model="form.description"
            label="وصف المهمة"
            placeholder="اكتب تفاصيل المهمة بالكامل — يظهر للمستلم مع العنوان"
            auto-grow
            rows="6"
            max-rows="20"
            counter
            :maxlength="20000"
          />
          <VTextField
            v-model="form.dueDate"
            label="تاريخ الاستحقاق"
            type="date"
          />
          <div class="d-flex flex-wrap gap-3">
            <VSelect
              v-model="form.dueHour"
              label="الساعة"
              :items="HOUR_ITEMS"
              item-title="title"
              item-value="value"
              style="min-width: 100px; flex: 1"
            />
            <VSelect
              v-model="form.dueMinute"
              label="الدقيقة"
              :items="MINUTE_ITEMS"
              item-title="title"
              item-value="value"
              style="min-width: 100px; flex: 1"
            />
            <VSelect
              v-model="form.duePeriod"
              label="الفترة"
              :items="PERIOD_ITEMS"
              item-title="title"
              item-value="value"
              style="min-width: 120px; flex: 1"
            />
          </div>
          <VSelect
            v-model="form.priority"
            label="الأولوية"
            :items="PRIORITY_ITEMS"
            item-title="title"
            item-value="value"
          />
          <VSelect
            v-model="form.assigneeOptionKey"
            label="المسند إليه"
            :items="assigneeOptions"
            item-title="title"
            item-value="value"
            :no-data-text="'لا يوجد مفوض أو مستخدمو فرع'"
            @update:model-value="onAssigneeChange"
          />
          <VSelect
            v-if="showCityPicker"
            v-model="form.assigneeCityValue"
            label="المحافظة"
            :items="cityPickerItems"
            item-title="title"
            item-value="value"
            :hint="cityPickerItems.length > 1 ? 'إدارة الفرع مسندة لأكثر من محافظة — اختر محافظة هذه المهمة' : ''"
            persistent-hint
            :rules="[v => !!v || 'المحافظة مطلوبة']"
          />
          <VFileInput
            label="مرفقات (صورة، PDF، Word، Excel)"
            accept="image/*,.pdf,.doc,.docx,.xls,.xlsx,application/pdf,application/msword,application/vnd.openxmlformats-officedocument.wordprocessingml.document,application/vnd.ms-excel,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            prepend-icon="tabler-paperclip"
            multiple
            show-size
            chips
            @update:model-value="onFilesChange"
          />
        </VCardText>
        <VCardActions class="justify-end pa-4">
          <VBtn
            variant="text"
            @click="createDialog = false"
          >
            إلغاء
          </VBtn>
          <VBtn
            color="primary"
            :loading="saving || uploading"
            @click="save"
          >
            {{ isEdit ? 'حفظ وإعادة إرسال' : 'إنشاء' }}
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>

    <!-- Detail + timeline -->
    <VDialog
      v-model="detailDialog"
      max-width="960"
      scrollable
    >
      <VCard :loading="detailLoading">
        <VCardTitle class="d-flex align-center justify-space-between flex-wrap gap-2">
          <span>{{ pick(detailTask, 'title', 'Title') || 'تفاصيل المهمة' }}</span>
          <div class="d-flex align-center gap-1">
            <VBtn
              v-if="canEditDetail"
              variant="tonal"
              color="warning"
              size="small"
              @click="openEdit(detailTask)"
            >
              تعديل وإعادة إرسال
            </VBtn>
            <VBtn
              icon
              variant="text"
              @click="detailDialog = false"
            >
              <VIcon icon="tabler-x" />
            </VBtn>
          </div>
        </VCardTitle>
        <VCardText
          v-if="detailTask"
          class="d-flex flex-column gap-4"
        >
          <div class="d-flex flex-wrap gap-2">
            <VChip
              size="small"
              :color="priorityColor(pick(detailTask, 'priority', 'Priority'))"
              variant="tonal"
            >
              {{ priorityLabel(pick(detailTask, 'priority', 'Priority')) }}
            </VChip>
            <VChip
              size="small"
              :color="statusColor(pick(detailTask, 'status', 'Status'))"
              variant="tonal"
            >
              {{ statusLabel(pick(detailTask, 'status', 'Status')) }}
            </VChip>
            <VChip
              v-if="isTaskOverdue(detailTask)"
              size="small"
              color="error"
            >
              تجاوزت المدة
            </VChip>
          </div>

          <div class="text-body-2">
            <div>الاستحقاق: {{ dueDisplay(detailTask) }}</div>
            <div>المسند: {{ pick(detailTask, 'assigneeDisplayName', 'AssigneeDisplayName') || '—' }}</div>
            <div v-if="pick(detailTask, 'assigneeCityValue', 'AssigneeCityValue')">
              المحافظة: {{ pick(detailTask, 'assigneeCityValue', 'AssigneeCityValue') }}
            </div>
            <div
              v-if="pick(detailTask, 'rejectNote', 'RejectNote')"
              class="text-error"
            >
              ملاحظة الرفض: {{ pick(detailTask, 'rejectNote', 'RejectNote') }}
            </div>
          </div>

          <div
            v-if="pick(detailTask, 'description', 'Description')"
            class="task-description"
          >
            <div class="text-subtitle-2 mb-1">
              وصف المهمة
            </div>
            <div class="text-body-2 whitespace-pre-wrap">
              {{ pick(detailTask, 'description', 'Description') }}
            </div>
          </div>

          <div>
            <div class="text-subtitle-2 mb-2">
              أوقات النقل والإنجاز
            </div>
            <div class="text-body-2 d-flex flex-column gap-1">
              <div>
                النقل:
                {{ stampDisplay(pick(detailTask, 'transferredAtUtc', 'TransferredAtUtc')) }}
              </div>
              <div>
                إنجاز الفرع:
                {{ stampDisplay(pick(detailTask, 'branchCompletedAtUtc', 'BranchCompletedAtUtc')) }}
              </div>
              <div>
                إنجاز المفوض:
                {{ stampDisplay(pick(detailTask, 'delegatedCompletedAtUtc', 'DelegatedCompletedAtUtc')) }}
              </div>
              <div>
                تصديق المفوض:
                {{ stampDisplay(pick(detailTask, 'delegatedConfirmedAtUtc', 'DelegatedConfirmedAtUtc')) }}
              </div>
            </div>
          </div>

          <div>
            <div class="d-flex flex-wrap align-center justify-space-between gap-2 mb-2">
              <div class="text-subtitle-2">
                المرفقات
              </div>
              <VFileInput
                label="إضافة مرفق"
                density="compact"
                hide-details
                accept="image/*,.pdf,.doc,.docx,.xls,.xlsx,application/pdf"
                prepend-icon="tabler-upload"
                style="max-width: 280px"
                :disabled="uploading"
                @update:model-value="uploadMoreInDetail"
              />
            </div>
            <div
              v-if="!(detail?.attachments || []).length"
              class="text-medium-emphasis text-body-2"
            >
              لا مرفقات.
            </div>
            <div
              v-else
              class="d-flex flex-column gap-1"
            >
              <div
                v-for="file in detail.attachments"
                :key="pick(file, 'id', 'Id')"
                class="d-flex align-center justify-space-between gap-2"
              >
                <span class="text-body-2">{{ pick(file, 'fileName', 'FileName') }}</span>
                <VBtn
                  size="small"
                  variant="text"
                  color="primary"
                  @click="downloadAttachment(file)"
                >
                  تنزيل
                </VBtn>
              </div>
            </div>
          </div>

          <div>
            <div class="text-subtitle-2 mb-2">
              الجدول الزمني
            </div>
            <VDataTable
              :headers="timelineHeaders"
              :items="timelineRows"
              item-value="id"
              density="compact"
              class="text-no-wrap"
              hide-default-footer
              :items-per-page="-1"
            />
          </div>
        </VCardText>
      </VCard>
    </VDialog>
  </div>
</template>

<style scoped>
.task-description {
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-radius: 12px;
  padding: 12px 14px;
  background: rgba(var(--v-theme-surface-variant), 0.25);
}
.whitespace-pre-wrap {
  white-space: pre-wrap;
  word-break: break-word;
}
.assigned-task-overdue {
  border-color: rgb(var(--v-theme-error)) !important;
  border-width: 2px !important;
  background: rgba(var(--v-theme-error), 0.14) !important;
  box-shadow: inset 5px 0 0 rgb(var(--v-theme-error));
}
.assigned-task-overdue :deep(.v-chip) {
  font-weight: 600;
}
</style>
