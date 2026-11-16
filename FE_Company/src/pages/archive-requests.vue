<script setup>
import { computed, ref, watch } from 'vue'
import { useToast } from '@/composables/useToast'
import ArchiveBranchSelect from '@/components/ArchiveBranchSelect.vue'
import {
  REQUEST_STATUS,
  archiveAssignRequest,
  archiveRecipients,
  archiveRequestsBoard,
  customerProfileRoute,
  errorMessage,
  formatDay,
  money,
} from '@/composables/archiveFlowApi'

const toast = useToast()
const city = ref('')
const loading = ref(false)
const tab = ref('requested')
const search = ref('')
const board = ref({ requested: [], atCommittee: [], sent: [], received: [] })
const recipients = ref([])

const target = ref(null)
const chosen = ref('')
const note = ref('')
const saving = ref(false)

const tabs = computed(() => [
  { key: 'requested', title: 'طلبات جديدة', count: board.value.requested.length },
  { key: 'atCommittee', title: 'عند اللجنة', count: board.value.atCommittee.length },
  { key: 'sent', title: 'سُلّمت للمتابع', count: board.value.sent.length },
  { key: 'received', title: 'تم الاستلام', count: board.value.received.length },
])

const rows = computed(() => {
  const list = board.value[tab.value] || []
  const q = search.value.trim()
  if (!q)
    return list

  return list.filter(r => [r.customerName, r.followerName, r.phoneNumber, r.listName, r.committeeUser, r.followerNote]
    .some(v => String(v || '').includes(q)))
})

const recipientItems = computed(() => recipients.value.map(r => ({
  title: `${r.name} — ${r.role === 'مسؤول لجنة المراقبة' ? 'مسؤول اللجنة' : 'مساعد مسؤول اللجنة'}`,
  value: r.name,
})))

async function load() {
  if (!city.value)
    return
  loading.value = true
  try {
    const [data, people] = await Promise.all([
      archiveRequestsBoard(city.value),
      archiveRecipients(city.value).catch(() => []),
    ])
    board.value = {
      requested: data?.requested || [],
      atCommittee: data?.atCommittee || [],
      sent: data?.sent || [],
      received: data?.received || [],
    }
    recipients.value = Array.isArray(people) ? people : []
  }
  catch (e) {
    toast.error(errorMessage(e, 'تعذر تحميل طلبات الأرشيف'))
  }
  finally {
    loading.value = false
  }
}

function openAssign(row) {
  target.value = row
  chosen.value = row.committeeUser || (recipients.value.length === 1 ? recipients.value[0].name : '')
  note.value = ''
}

async function confirmAssign() {
  if (!target.value || !chosen.value)
    return
  saving.value = true
  try {
    await archiveAssignRequest(city.value, target.value.id, chosen.value, note.value.trim())
    toast.success(`أُسند أرشيف ${target.value.customerName || ''} إلى ${chosen.value}`)
    target.value = null
    await load()
  }
  catch (e) {
    toast.error(errorMessage(e, 'تعذر الإسناد'))
  }
  finally {
    saving.value = false
  }
}

watch(city, () => {
  board.value = { requested: [], atCommittee: [], sent: [], received: [] }
  load()
})
</script>

<template>
  <div>
    <div class="d-flex flex-wrap align-center justify-space-between gap-3 mb-4">
      <div>
        <h1 class="text-h5 font-weight-bold mb-1">
          طلبات أرشيف المتابعين
        </h1>
        <p class="text-body-2 text-medium-emphasis mb-0">
          أسند الطلب لمسؤول اللجنة أو مساعده، يستلمه ثم يسلّمه للمتابع
        </p>
      </div>
      <div class="d-flex align-center gap-2">
        <ArchiveBranchSelect v-model="city" />
        <VBtn
          icon="tabler-refresh"
          variant="tonal"
          :loading="loading"
          @click="load"
        />
      </div>
    </div>

    <VCard>
      <VTabs
        v-model="tab"
        grow
      >
        <VTab
          v-for="t in tabs"
          :key="t.key"
          :value="t.key"
        >
          {{ t.title }}
          <VChip
            size="x-small"
            class="ms-2"
            :color="t.key === 'requested' && t.count ? 'warning' : undefined"
          >
            {{ t.count }}
          </VChip>
        </VTab>
      </VTabs>
      <VDivider />

      <VCardText>
        <VTextField
          v-model="search"
          prepend-inner-icon="tabler-search"
          placeholder="بحث باسم الزبون أو المتابع"
          density="compact"
          hide-details
          clearable
          class="mb-4"
          style="max-inline-size: 360px;"
        />

        <div
          v-if="loading && !rows.length"
          class="text-center py-10"
        >
          <VProgressCircular
            indeterminate
            color="primary"
          />
        </div>

        <div
          v-else-if="!rows.length"
          class="text-center text-medium-emphasis py-10"
        >
          <VIcon
            icon="tabler-archive"
            size="40"
            class="mb-2"
          />
          <div>لا توجد طلبات هنا</div>
        </div>

        <div
          v-for="row in rows"
          v-else
          :key="row.id"
          class="request-row"
        >
          <div class="flex-grow-1 min-w-0">
            <div>
              <RouterLink
                :to="customerProfileRoute(city, row)"
                class="font-weight-bold"
              >
                {{ row.customerName || `زبون ${row.customerId}` }}
              </RouterLink>
              <VChip
                size="x-small"
                class="ms-2"
                :color="row.remaining > 0 ? 'error' : 'success'"
              >
                {{ row.remaining > 0 ? `المتبقي ${money(row.remaining)}` : 'منتهٍ' }}
              </VChip>
            </div>
            <div class="text-body-2 text-medium-emphasis">
              المتابع: {{ row.followerName || '—' }}
              <span v-if="row.listName"> · {{ row.listName }}</span>
              <span v-if="row.phoneNumber"> · {{ row.phoneNumber }}</span>
            </div>
            <div class="text-caption text-medium-emphasis">
              طُلب {{ formatDay(row.requestedAtUtc) }}
              <span v-if="row.committeeUser"> · اللجنة: {{ row.committeeUser }}</span>
              <span v-if="row.sentAtUtc"> · سُلّم {{ formatDay(row.sentAtUtc) }}</span>
              <span v-if="row.receivedAtUtc"> · استُلم {{ formatDay(row.receivedAtUtc) }}</span>
            </div>
            <div
              v-if="row.followerNote"
              class="text-body-2"
            >
              ملاحظة المتابع: {{ row.followerNote }}
            </div>
          </div>
          <VBtn
            v-if="row.status === 'Requested' || row.status === 'ToCommittee'"
            :color="row.status === 'Requested' ? 'primary' : undefined"
            :variant="row.status === 'Requested' ? 'elevated' : 'tonal'"
            prepend-icon="tabler-send"
            @click="openAssign(row)"
          >
            {{ row.status === 'Requested' ? 'إسناد' : 'تغيير' }}
          </VBtn>
          <VChip
            v-if="row.status !== 'Requested'"
            size="small"
            :color="REQUEST_STATUS[row.status]?.color"
          >
            {{ REQUEST_STATUS[row.status]?.label || row.status }}
          </VChip>
        </div>
      </VCardText>
    </VCard>

    <VDialog
      :model-value="!!target"
      max-width="460"
      @update:model-value="v => { if (!v) target = null }"
    >
      <VCard v-if="target">
        <VCardTitle class="pt-5">
          إسناد الأرشيف
        </VCardTitle>
        <VCardText>
          <p class="mb-4">
            أرشيف <b>{{ target.customerName }}</b> للمتابع <b>{{ target.followerName }}</b>
          </p>
          <p
            v-if="target.followerNote"
            class="mb-4 text-body-2"
          >
            ملاحظة المتابع: {{ target.followerNote }}
          </p>
          <VAlert
            v-if="!recipients.length"
            type="warning"
            variant="tonal"
            class="mb-3"
          >
            لا يوجد مسؤول لجنة مراقبة أو مساعد لهذا الفرع
          </VAlert>
          <VSelect
            v-model="chosen"
            :items="recipientItems"
            label="يُسلَّم إلى"
            class="mb-3"
            hide-details
          />
          <VTextField
            v-model="note"
            label="ملاحظة (اختياري)"
            maxlength="500"
            hide-details
          />
        </VCardText>
        <VCardActions>
          <VSpacer />
          <VBtn
            variant="text"
            @click="target = null"
          >
            إلغاء
          </VBtn>
          <VBtn
            color="primary"
            variant="elevated"
            :disabled="!chosen"
            :loading="saving"
            @click="confirmAssign"
          >
            تأكيد
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>
  </div>
</template>

<style scoped>
.request-row {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 12px 4px;
  border-block-end: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}

.request-row:last-child {
  border-block-end: none;
}

.min-w-0 {
  min-inline-size: 0;
}
</style>
