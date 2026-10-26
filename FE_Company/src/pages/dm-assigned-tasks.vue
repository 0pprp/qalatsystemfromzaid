<script setup>
import { computed, onMounted, ref } from 'vue'
import {
  dmAssignedTaskAccept,
  dmAssignedTaskComplete,
  dmAssignedTaskConfirm,
  dmAssignedTaskReject,
  dmAssignedTaskTransfer,
  dmAssignedTasksInbox,
  dmBmUsersList,
} from '@/composables/delegatedManagerApi'
import { useToast } from '@/composables/useToast'

defineOptions({ name: 'DmAssignedTasks' })
definePage({ meta: { layout: 'dm' } })

const toast = useToast()
const busy = ref(false)
const rows = ref([])
const actionBusy = ref(false)

const rejectDialog = ref(false)
const rejectNote = ref('')
const rejectId = ref('')

const transferDialog = ref(false)
const transferId = ref('')
const transferNote = ref('')
const transferTarget = ref(null)
const bmOptions = ref([])

function pick(obj, ...keys) {
  if (!obj) return undefined
  for (const k of keys) {
    if (obj[k] != null && obj[k] !== '')
      return obj[k]
  }
  return undefined
}

function actionsFor(status) {
  const s = String(status || '').trim()
  if (s === 'مرسلة')
    return { accept: true, reject: true }
  if (s === 'مقبولة')
    return { transfer: true, complete: true }
  if (s === 'بانتظار_تصديق_المفوض')
    return { confirm: true }
  return {}
}

function statusLabel(s) {
  if (s === 'بانتظار_تصديق_المفوض')
    return 'بانتظار تصديق المفوض'
  return s || '—'
}

async function load() {
  busy.value = true
  try {
    const data = await dmAssignedTasksInbox()
    rows.value = Array.isArray(data) ? data : (pick(data, 'items', 'Items') || [])
  }
  catch (e) {
    rows.value = []
    toast.error(e?.response?.data?.message || e?.message || 'تعذر تحميل المهام')
  }
  finally {
    busy.value = false
  }
}

async function run(fn, okMsg) {
  actionBusy.value = true
  try {
    await fn()
    toast.success(okMsg)
    await load()
  }
  catch (e) {
    toast.error(e?.response?.data?.message || e?.message || 'تعذر تنفيذ الإجراء')
  }
  finally {
    actionBusy.value = false
  }
}

function openReject(id) {
  rejectId.value = id
  rejectNote.value = ''
  rejectDialog.value = true
}

async function confirmReject() {
  if (!rejectNote.value.trim()) {
    toast.warning('ملاحظة الرفض مطلوبة')
    return
  }
  rejectDialog.value = false
  await run(() => dmAssignedTaskReject(rejectId.value, rejectNote.value), 'تم الرفض')
}

async function openTransfer(id) {
  transferId.value = id
  transferNote.value = ''
  transferTarget.value = null
  transferDialog.value = true
  try {
    const data = await dmBmUsersList()
    const list = Array.isArray(data) ? data : (pick(data, 'items', 'Items') || [])
    bmOptions.value = list
      .filter(u => pick(u, 'isActive', 'IsActive') !== false)
      .map(u => ({
        title: pick(u, 'displayName', 'DisplayName') || pick(u, 'userName', 'UserName') || 'مستخدم',
        value: pick(u, 'id', 'Id'),
        raw: u,
      }))
  }
  catch {
    bmOptions.value = []
  }
}

async function confirmTransfer() {
  const opt = bmOptions.value.find(o => o.value === transferTarget.value)
  if (!opt) {
    toast.warning('اختر مدير فرع')
    return
  }
  transferDialog.value = false
  await run(
    () => dmAssignedTaskTransfer(transferId.value, {
      targetUserId: String(opt.value),
      targetDisplayName: opt.title,
      note: transferNote.value || undefined,
    }),
    'تم النقل',
  )
}

const empty = computed(() => !busy.value && !rows.value.length)

onMounted(load)
</script>

<template>
  <div>
    <div class="d-flex align-center justify-space-between mb-4 flex-wrap gap-2">
      <h1 class="text-h5 mb-0">
        مهامي
      </h1>
      <VBtn
        variant="tonal"
        :loading="busy"
        @click="load"
      >
        تحديث
      </VBtn>
    </div>

    <VProgressLinear
      v-if="busy"
      indeterminate
      class="mb-3"
    />
    <VAlert
      v-if="empty"
      type="info"
      variant="tonal"
    >
      لا مهام في الوارد حالياً
    </VAlert>

    <div class="dm-task-list">
      <VCard
        v-for="row in rows"
        :key="pick(row, 'id', 'Id')"
        variant="outlined"
        class="mb-3"
      >
        <VCardText>
          <div class="d-flex flex-wrap justify-space-between gap-2 mb-2">
            <strong>{{ pick(row, 'title', 'Title') || 'مهمة' }}</strong>
            <VChip
              size="small"
              color="primary"
              variant="tonal"
            >
              {{ statusLabel(pick(row, 'status', 'Status')) }}
            </VChip>
          </div>
          <p class="text-body-2 text-medium-emphasis mb-2">
            {{ pick(row, 'body', 'Body') || pick(row, 'description', 'Description') || '—' }}
          </p>
          <div class="text-caption text-medium-emphasis mb-3">
            أولوية: {{ pick(row, 'priority', 'Priority') ?? '—' }}
            ·
            {{ pick(row, 'cityName', 'CityName') || '—' }}
          </div>
          <div class="d-flex flex-wrap gap-2">
            <VBtn
              v-if="actionsFor(pick(row, 'status', 'Status')).accept"
              size="small"
              color="success"
              :loading="actionBusy"
              @click="run(() => dmAssignedTaskAccept(pick(row, 'id', 'Id')), 'تم القبول')"
            >
              قبول
            </VBtn>
            <VBtn
              v-if="actionsFor(pick(row, 'status', 'Status')).reject"
              size="small"
              color="error"
              variant="tonal"
              :disabled="actionBusy"
              @click="openReject(pick(row, 'id', 'Id'))"
            >
              رفض
            </VBtn>
            <VBtn
              v-if="actionsFor(pick(row, 'status', 'Status')).transfer"
              size="small"
              variant="tonal"
              :disabled="actionBusy"
              @click="openTransfer(pick(row, 'id', 'Id'))"
            >
              نقل
            </VBtn>
            <VBtn
              v-if="actionsFor(pick(row, 'status', 'Status')).complete"
              size="small"
              color="primary"
              :loading="actionBusy"
              @click="run(() => dmAssignedTaskComplete(pick(row, 'id', 'Id')), 'تم الإنجاز')"
            >
              إنجاز
            </VBtn>
            <VBtn
              v-if="actionsFor(pick(row, 'status', 'Status')).confirm"
              size="small"
              color="primary"
              :loading="actionBusy"
              @click="run(() => dmAssignedTaskConfirm(pick(row, 'id', 'Id')), 'تم التصديق')"
            >
              تصديق
            </VBtn>
          </div>
        </VCardText>
      </VCard>
    </div>

    <VDialog
      v-model="rejectDialog"
      max-width="420"
    >
      <VCard>
        <VCardTitle>رفض المهمة</VCardTitle>
        <VCardText>
          <VTextarea
            v-model="rejectNote"
            label="ملاحظة الرفض *"
            rows="3"
          />
        </VCardText>
        <VCardActions>
          <VSpacer />
          <VBtn
            variant="text"
            @click="rejectDialog = false"
          >
            إلغاء
          </VBtn>
          <VBtn
            color="error"
            @click="confirmReject"
          >
            رفض
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>

    <VDialog
      v-model="transferDialog"
      max-width="480"
    >
      <VCard>
        <VCardTitle>نقل إلى مدير فرع</VCardTitle>
        <VCardText>
          <VSelect
            v-model="transferTarget"
            :items="bmOptions"
            item-title="title"
            item-value="value"
            label="مدير الفرع"
            class="mb-3"
          />
          <VTextField
            v-model="transferNote"
            label="ملاحظة (اختياري)"
          />
        </VCardText>
        <VCardActions>
          <VSpacer />
          <VBtn
            variant="text"
            @click="transferDialog = false"
          >
            إلغاء
          </VBtn>
          <VBtn
            color="primary"
            @click="confirmTransfer"
          >
            نقل
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>
  </div>
</template>
