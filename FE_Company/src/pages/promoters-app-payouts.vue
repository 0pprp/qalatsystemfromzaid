<script setup>
import { nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import {
  PA_PAYOUT_STATUSES,
  paDateTime,
  paErrorMessage,
  paGetPayout,
  paListPayouts,
  paMarkPaid,
  paPayoutReceiptUrl,
  paMeta,
  paMoney,
  paPaged,
  paPayoutCounts,
  paPayoutMessages,
  paRejectPayout,
  paMessageAttachmentUrl,
  paSendPayoutAttachment,
  paSendPayoutMessage,
} from '@/composables/promotersAppApi'
import { useToast } from '@/composables/useToast'

definePage({ meta: { layout: 'default' } })

const toast = useToast()

const rows = ref([])
const total = ref(0)
const page = ref(1)
const loading = ref(false)
const listError = ref('')
const tab = ref('Pending')
const counts = ref({})
const search = ref('')
let gen = 0
let listTimer = null

const headers = [
  { title: 'المروّج', key: 'username', sortable: false },
  { title: 'المبلغ', key: 'amount', sortable: false },
  { title: 'الاستلام', key: 'method', sortable: false },
  { title: 'التاريخ', key: 'createdAt', sortable: false },
  { title: 'المحادثة', key: 'chat', sortable: false },
  { title: 'الحالة', key: 'status', sortable: false },
]

async function load(silent = false) {
  const g = ++gen

  if (!silent)
    loading.value = true
  listError.value = ''
  try {
    const [res, c] = await Promise.all([
      paListPayouts({ status: tab.value === 'all' ? '' : tab.value, q: search.value, page: page.value, pageSize: 20 }),
      paPayoutCounts(),
    ])

    if (g !== gen)
      return
    const p = paPaged(res)

    rows.value = p.items
    total.value = p.total
    counts.value = c || {}
  }
  catch (err) {
    if (g === gen && !silent)
      listError.value = paErrorMessage(err, 'تعذر تحميل طلبات السحب')
  }
  finally {
    if (g === gen)
      loading.value = false
  }
}

watch([tab, search], () => { if (page.value !== 1) page.value = 1; else load() })
watch(page, () => load())

// ---------------------------------------------------------------- detail + chat
const open = ref(false)
const detail = ref(null)
const messages = ref([])
const draft = ref('')
const sending = ref(false)
const chatBox = ref(null)
let chatTimer = null

function scrollChat() {
  nextTick(() => {
    const el = chatBox.value

    if (el)
      el.scrollTop = el.scrollHeight
  })
}

async function openDetail(row) {
  try {
    const d = await paGetPayout(row.id)

    detail.value = d.payout
    messages.value = d.messages
    draft.value = ''
    open.value = true
    scrollChat()
    clearInterval(chatTimer)
    chatTimer = setInterval(pollChat, 5000)
    load(true)
  }
  catch (err) {
    toast.error(paErrorMessage(err))
  }
}

async function pollChat() {
  if (!open.value || !detail.value)
    return
  try {
    const lastId = messages.value.length ? messages.value[messages.value.length - 1].id : 0
    const fresh = await paPayoutMessages(detail.value.id, lastId)

    if (fresh?.length) {
      messages.value.push(...fresh.filter(m => !messages.value.some(x => x.id === m.id)))
      scrollChat()
    }
  }
  catch { /* keep polling */ }
}

watch(open, v => {
  if (!v) {
    clearAttachmentUrls()
    clearInterval(chatTimer)
    load(true)
  }
})

async function send() {
  const body = draft.value.trim()
  if (!body)
    return
  sending.value = true
  try {
    const m = await paSendPayoutMessage(detail.value.id, body)

    messages.value.push(m)
    draft.value = ''
    scrollChat()
  }
  catch (err) {
    toast.error(paErrorMessage(err))
  }
  finally {
    sending.value = false
  }
}

const decision = ref(null) // 'paid' | 'reject'
const decisionNote = ref('')
const decisionBusy = ref(false)
const decisionError = ref('')

// «تم التحويل» needs a picture of the transfer receipt.
const receiptInput = ref(null)
const receiptFile = ref(null)
const receiptPreview = ref('')

function clearReceiptPick() {
  if (receiptPreview.value)
    URL.revokeObjectURL(receiptPreview.value)
  receiptFile.value = null
  receiptPreview.value = ''
}

function onReceiptPicked(e) {
  const file = e.target.files?.[0]

  e.target.value = ''
  if (!file)
    return
  if (!['image/jpeg', 'image/png', 'image/webp'].includes(file.type)) {
    decisionError.value = 'اختر صورة (JPG أو PNG أو WEBP)'

    return
  }
  if (file.size > 10 * 1024 * 1024) {
    decisionError.value = 'حجم الصورة يجب ألا يتجاوز 10 ميغابايت'

    return
  }
  clearReceiptPick()
  decisionError.value = ''
  receiptFile.value = file
  receiptPreview.value = URL.createObjectURL(file)
}

function startDecision(kind) {
  decision.value = kind
  decisionNote.value = ''
  decisionError.value = ''
  clearReceiptPick()
}

async function confirmDecision() {
  if (decision.value === 'paid' && !receiptFile.value) {
    decisionError.value = 'أرفق صورة وصل التحويل'

    return
  }
  decisionBusy.value = true
  decisionError.value = ''
  try {
    const d = decision.value === 'paid'
      ? await paMarkPaid(detail.value.id, receiptFile.value, decisionNote.value)
      : await paRejectPayout(detail.value.id, decisionNote.value)

    detail.value = d.payout
    messages.value = d.messages
    toast.success(decision.value === 'paid' ? 'تم تأكيد التحويل وإرسال الوصل للمروّج' : 'تم رفض الطلب وإرجاع المبلغ إلى رصيده')
    decision.value = null
    clearReceiptPick()
    loadReceipt()
    load(true)
  }
  catch (err) {
    decisionError.value = paErrorMessage(err)
  }
  finally {
    decisionBusy.value = false
  }
}

// Photos and documents in the chat (private: fetched with the dashboard token).
const attachInput = ref(null)
const uploadingAttachment = ref(false)
const attachmentUrls = ref({})

function fileSize(bytes) {
  const n = Number(bytes) || 0

  return n < 1024 * 1024 ? `${Math.max(1, Math.round(n / 1024))} KB` : `${(n / 1024 / 1024).toFixed(1)} MB`
}

async function loadAttachmentImages() {
  if (!detail.value)
    return
  for (const m of messages.value) {
    if (m.attachmentKind !== 'image' || attachmentUrls.value[m.id])
      continue
    try {
      attachmentUrls.value = { ...attachmentUrls.value, [m.id]: await paMessageAttachmentUrl(detail.value.id, m.id) }
    }
    catch { /* stays as "loading" */ }
  }
}

function clearAttachmentUrls() {
  Object.values(attachmentUrls.value).forEach(u => URL.revokeObjectURL(u))
  attachmentUrls.value = {}
}

watch(() => messages.value.map(m => m.id).join(','), loadAttachmentImages)

function viewAttachment(m) {
  const url = attachmentUrls.value[m.id]
  if (url)
    window.open(url, '_blank', 'noopener')
}

async function downloadAttachment(m) {
  try {
    const url = await paMessageAttachmentUrl(detail.value.id, m.id)
    const a = document.createElement('a')

    a.href = url
    a.download = m.attachmentName || `attachment-${m.id}`
    document.body.appendChild(a)
    a.click()
    a.remove()
    setTimeout(() => URL.revokeObjectURL(url), 5000)
  }
  catch (err) {
    toast.error(paErrorMessage(err, 'تعذر تحميل الملف'))
  }
}

async function onAttachPicked(e) {
  const file = e.target.files?.[0]

  e.target.value = ''
  if (!file || !detail.value)
    return
  if (file.size > 15 * 1024 * 1024) {
    toast.error('حجم الملف يجب ألا يتجاوز 15 ميغابايت')

    return
  }
  uploadingAttachment.value = true
  try {
    const m = await paSendPayoutAttachment(detail.value.id, file, draft.value.trim())

    messages.value.push(m)
    draft.value = ''
    scrollChat()
  }
  catch (err) {
    toast.error(paErrorMessage(err, 'تعذر إرسال الملف'))
  }
  finally {
    uploadingAttachment.value = false
  }
}

// The saved receipt of a paid request (fetched with the dashboard token).
const receiptUrl = ref('')
const receiptOpen = ref(false)

async function loadReceipt() {
  if (receiptUrl.value)
    URL.revokeObjectURL(receiptUrl.value)
  receiptUrl.value = ''
  if (!detail.value?.hasReceipt)
    return
  try {
    receiptUrl.value = await paPayoutReceiptUrl(detail.value.id)
  }
  catch { /* shown as "not available" */ }
}

watch(() => detail.value?.id, loadReceipt)

function copy(text) {
  navigator.clipboard?.writeText(String(text)).then(() => toast.success('تم النسخ'))
}

onMounted(() => {
  load()
  listTimer = setInterval(() => load(true), 30000)
})
onBeforeUnmount(() => {
  clearInterval(listTimer)
  clearInterval(chatTimer)
  clearReceiptPick()
  if (receiptUrl.value)
    URL.revokeObjectURL(receiptUrl.value)
  clearAttachmentUrls()
})
</script>

<template>
  <div>
    <div class="d-flex flex-wrap align-center justify-space-between gap-3 mb-4">
      <div>
        <h4 class="text-h4 mb-1 d-flex align-center gap-2">
          <VIcon
            icon="tabler-wallet"
            color="primary"
          />
          طلبات السحب
          <VChip
            v-if="counts.unreadMessages"
            color="error"
            size="small"
          >
            {{ counts.unreadMessages }} رسالة جديدة
          </VChip>
        </h4>
        <p class="text-medium-emphasis mb-0">
          يطلب المروّج سحب أرباحه إلى كي كارد أو زين كاش. المبلغ يُحجز من رصيده، وعند الرفض يعود إليه.
        </p>
      </div>
      <div class="d-flex gap-2 align-center">
        <VTextField
          v-model="search"
          density="compact"
          label="بحث"
          prepend-inner-icon="tabler-search"
          clearable
          style="min-width: 220px;"
          hide-details
        />
        <VBtn
          variant="tonal"
          prepend-icon="tabler-refresh"
          :loading="loading"
          @click="load"
        >
          تحديث
        </VBtn>
      </div>
    </div>

    <VAlert
      v-if="listError"
      type="error"
      variant="tonal"
      class="mb-4"
    >
      {{ listError }}
    </VAlert>

    <VCard>
      <VTabs v-model="tab">
        <VTab value="Pending">
          قيد المعالجة <VChip
            size="x-small"
            color="warning"
            class="ms-2"
          >
            {{ counts.pending || 0 }}
          </VChip>
        </VTab>
        <VTab value="Paid">
          تم التحويل <VChip
            size="x-small"
            class="ms-2"
          >
            {{ counts.paid || 0 }}
          </VChip>
        </VTab>
        <VTab value="Rejected">
          مرفوضة <VChip
            size="x-small"
            class="ms-2"
          >
            {{ counts.rejected || 0 }}
          </VChip>
        </VTab>
        <VTab value="all">
          الكل
        </VTab>
      </VTabs>
      <VDivider />
      <VDataTableServer
        v-model:page="page"
        :items-per-page="20"
        :headers="headers"
        :items="rows"
        :items-length="total"
        :loading="loading"
        hover
        no-data-text="لا توجد طلبات"
        @click:row="(_, { item }) => openDetail(item)"
      >
        <template #item.username="{ item }">
          <code dir="auto">{{ item.username }}</code>
          <div
            class="text-caption text-medium-emphasis"
            dir="ltr"
            style="text-align: end;"
          >
            {{ item.phone }}
          </div>
        </template>
        <template #item.amount="{ item }">
          <strong>{{ paMoney(item.amount) }}</strong>
        </template>
        <template #item.method="{ item }">
          {{ item.methodLabel }}
          <div
            class="text-caption"
            dir="ltr"
            style="text-align: end;"
          >
            {{ item.accountNumber }}
          </div>
        </template>
        <template #item.createdAt="{ item }">
          <span class="text-no-wrap">{{ paDateTime(item.createdAt) }}</span>
        </template>
        <template #item.chat="{ item }">
          <VChip
            v-if="item.unreadMessages"
            color="error"
            size="small"
            prepend-icon="tabler-message"
          >
            {{ item.unreadMessages }} جديدة
          </VChip>
          <span
            v-else-if="item.lastMessageAt"
            class="text-caption text-medium-emphasis"
          >آخر رسالة {{ paDateTime(item.lastMessageAt) }}</span>
          <span
            v-else
            class="text-caption text-medium-emphasis"
          >—</span>
        </template>
        <template #item.status="{ item }">
          <VChip
            :color="paMeta(PA_PAYOUT_STATUSES, item.status).color"
            size="small"
            variant="tonal"
          >
            {{ item.statusLabel }}
          </VChip>
        </template>
      </VDataTableServer>
    </VCard>

    <VDialog
      v-model="open"
      max-width="620"
      scrollable
    >
      <VCard v-if="detail">
        <VCardTitle class="d-flex align-center gap-2 pt-4">
          طلب سحب #{{ detail.id }}
          <VChip
            :color="paMeta(PA_PAYOUT_STATUSES, detail.status).color"
            size="small"
            variant="tonal"
          >
            {{ detail.statusLabel }}
          </VChip>
          <VSpacer />
          <IconBtn @click="open = false">
            <VIcon icon="tabler-x" />
          </IconBtn>
        </VCardTitle>
        <VCardText>
          <VTable
            density="compact"
            class="mb-3"
          >
            <tbody>
              <tr>
                <td class="text-medium-emphasis">
                  المروّج
                </td><td><code dir="auto">{{ detail.username }}</code> — <span dir="ltr">{{ detail.phone }}</span></td>
              </tr>
              <tr>
                <td class="text-medium-emphasis">
                  المبلغ
                </td><td><strong class="text-h6">{{ paMoney(detail.amount) }}</strong></td>
              </tr>
              <tr>
                <td class="text-medium-emphasis">
                  {{ detail.methodLabel }}
                </td>
                <td>
                  <strong dir="ltr">{{ detail.accountNumber }}</strong>
                  <IconBtn
                    size="small"
                    @click="copy(detail.accountNumber)"
                  >
                    <VIcon
                      icon="tabler-copy"
                      size="18"
                    />
                  </IconBtn>
                </td>
              </tr>
              <tr>
                <td class="text-medium-emphasis">
                  الرصيد المتبقي
                </td><td>{{ paMoney(detail.balance) }}</td>
              </tr>
              <tr>
                <td class="text-medium-emphasis">
                  تاريخ الطلب
                </td><td>{{ paDateTime(detail.createdAt) }}</td>
              </tr>
              <tr v-if="detail.decidedBy">
                <td class="text-medium-emphasis">
                  المعالجة
                </td><td>{{ detail.decidedBy }} — {{ paDateTime(detail.decidedAt) }}<span v-if="detail.adminNote"> · {{ detail.adminNote }}</span></td>
              </tr>
            </tbody>
          </VTable>

          <div
            v-if="detail.hasReceipt"
            class="d-flex align-center gap-3 mb-4"
          >
            <span class="text-medium-emphasis">وصل التحويل</span>
            <img
              v-if="receiptUrl"
              :src="receiptUrl"
              alt="وصل التحويل"
              class="pa-receipt-thumb cursor-pointer"
              @click="receiptOpen = true"
            >
            <span
              v-else
              class="text-caption text-medium-emphasis"
            >جاري تحميل الوصل…</span>
          </div>

          <div
            v-if="detail.status === 'Pending'"
            class="d-flex gap-2 mb-4"
          >
            <VBtn
              color="success"
              prepend-icon="tabler-check"
              class="flex-grow-1"
              @click="startDecision('paid')"
            >
              تم التحويل
            </VBtn>
            <VBtn
              color="error"
              variant="tonal"
              prepend-icon="tabler-x"
              @click="startDecision('reject')"
            >
              رفض
            </VBtn>
          </div>

          <div class="font-weight-medium mb-2">
            المتابعة مع المروّج
          </div>
          <div
            ref="chatBox"
            class="pa-chat pa-3 rounded mb-2"
          >
            <div
              v-if="!messages.length"
              class="text-center text-medium-emphasis text-caption py-6"
            >
              لا توجد رسائل بعد
            </div>
            <div
              v-for="m in messages"
              :key="m.id"
              class="d-flex mb-2"
              :class="m.fromAdmin ? 'justify-start' : 'justify-end'"
            >
              <div
                class="pa-bubble"
                :class="m.fromAdmin ? 'pa-bubble--admin' : 'pa-bubble--user'"
              >
                <div class="text-caption font-weight-medium mb-1">
                  {{ m.fromAdmin ? 'الإدارة' : m.senderName }}
                </div>
                <template v-if="m.attachmentKind === 'image'">
                  <img
                    v-if="attachmentUrls[m.id]"
                    :src="attachmentUrls[m.id]"
                    :alt="m.attachmentName"
                    class="pa-chat-image cursor-pointer"
                    @click="viewAttachment(m)"
                  >
                  <div
                    v-else
                    class="pa-chat-image d-flex align-center justify-center text-caption text-medium-emphasis"
                  >
                    جاري التحميل…
                  </div>
                </template>
                <VChip
                  v-else-if="m.attachmentKind === 'file'"
                  prepend-icon="tabler-file-download"
                  variant="tonal"
                  class="mb-1"
                  @click="downloadAttachment(m)"
                >
                  {{ m.attachmentName }} <span class="text-caption ms-1">({{ fileSize(m.attachmentSize) }})</span>
                </VChip>
                <div
                  v-if="m.body"
                  style="white-space: pre-wrap;"
                >
                  {{ m.body }}
                </div>
                <div class="text-caption text-medium-emphasis mt-1">
                  {{ paDateTime(m.createdAt) }}
                </div>
              </div>
            </div>
          </div>
          <div class="d-flex gap-2 align-end">
            <VBtn
              icon="tabler-paperclip"
              variant="text"
              :loading="uploadingAttachment"
              title="إرسال صورة أو مستند"
              @click="attachInput?.click()"
            />
            <input
              ref="attachInput"
              type="file"
              accept="image/jpeg,image/png,image/webp,application/pdf,.doc,.docx,.xls,.xlsx"
              hidden
              @change="onAttachPicked"
            >
            <VTextarea
              v-model="draft"
              rows="1"
              auto-grow
              max-rows="4"
              label="اكتب رداً"
              hide-details
              @keydown.enter.exact.prevent="send"
            />
            <VBtn
              color="primary"
              icon="tabler-send"
              :loading="sending"
              :disabled="!draft.trim()"
              @click="send"
            />
          </div>
        </VCardText>
      </VCard>
    </VDialog>

    <VDialog
      :model-value="!!decision"
      max-width="420"
      @update:model-value="v => { if (!v) decision = null }"
    >
      <VCard :title="decision === 'paid' ? 'تأكيد التحويل' : 'رفض طلب السحب'">
        <VCardText>
          <VAlert
            v-if="decisionError"
            type="error"
            variant="tonal"
            density="compact"
            class="mb-3"
          >
            {{ decisionError }}
          </VAlert>
          <p
            v-if="decision === 'paid'"
            class="mb-3"
          >
            تأكد أنك حوّلت {{ paMoney(detail?.amount) }} إلى {{ detail?.methodLabel }} <span dir="ltr">{{ detail?.accountNumber }}</span>.
          </p>
          <p
            v-else
            class="mb-3"
          >
            سيعود المبلغ إلى رصيد المروّج.
          </p>

          <template v-if="decision === 'paid'">
            <div class="font-weight-medium mb-2">
              صورة وصل التحويل <span class="text-error">*</span>
            </div>
            <div
              v-if="receiptPreview"
              class="mb-2 text-center"
            >
              <img
                :src="receiptPreview"
                alt="وصل التحويل"
                class="pa-receipt-preview"
              >
            </div>
            <VBtn
              block
              variant="tonal"
              :color="receiptFile ? 'default' : 'primary'"
              prepend-icon="tabler-photo-up"
              class="mb-4"
              @click="receiptInput?.click()"
            >
              {{ receiptFile ? 'تغيير الصورة' : 'اختر صورة الوصل' }}
            </VBtn>
            <input
              ref="receiptInput"
              type="file"
              accept="image/jpeg,image/png,image/webp"
              hidden
              @change="onReceiptPicked"
            >
          </template>

          <VTextField
            v-model="decisionNote"
            :label="decision === 'paid' ? 'ملاحظة / رقم الحوالة (اختياري)' : 'سبب الرفض (يظهر للمروّج)'"
          />
        </VCardText>
        <VCardActions>
          <VSpacer />
          <VBtn
            variant="text"
            @click="decision = null"
          >
            إلغاء
          </VBtn>
          <VBtn
            :color="decision === 'paid' ? 'success' : 'error'"
            :loading="decisionBusy"
            :disabled="decision === 'paid' && !receiptFile"
            @click="confirmDecision"
          >
            تأكيد
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>

    <VDialog
      v-model="receiptOpen"
      max-width="720"
    >
      <VCard title="وصل التحويل">
        <VCardText class="text-center">
          <img
            v-if="receiptUrl"
            :src="receiptUrl"
            alt="وصل التحويل"
            style="max-width: 100%; max-height: 75vh;"
          >
        </VCardText>
        <VCardActions>
          <VSpacer />
          <VBtn
            v-if="receiptUrl"
            :href="receiptUrl"
            :download="`receipt-${detail?.id}`"
            prepend-icon="tabler-download"
            variant="tonal"
          >
            تحميل
          </VBtn>
          <VBtn
            variant="text"
            @click="receiptOpen = false"
          >
            إغلاق
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>
  </div>
</template>

<style scoped>
.pa-chat-image {
  display: block;
  width: 180px;
  max-width: 100%;
  height: 140px;
  object-fit: cover;
  border-radius: 8px;
  margin-bottom: 4px;
  background: rgba(var(--v-theme-on-surface), 0.06);
}

.pa-receipt-thumb {
  width: 72px;
  height: 72px;
  object-fit: cover;
  border-radius: 8px;
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}

.pa-receipt-preview {
  max-width: 100%;
  max-height: 260px;
  border-radius: 8px;
}

.pa-chat {
  max-height: 320px;
  min-height: 140px;
  overflow-y: auto;
  background: rgba(var(--v-theme-on-surface), 0.04);
}

.pa-bubble {
  max-width: 80%;
  padding: 8px 12px;
  border-radius: 12px;
}

.pa-bubble--admin {
  background: rgba(var(--v-theme-primary), 0.12);
}

.pa-bubble--user {
  background: rgb(var(--v-theme-surface));
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
</style>
