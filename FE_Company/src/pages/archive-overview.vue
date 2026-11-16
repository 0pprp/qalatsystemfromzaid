<script setup>
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useToast } from '@/composables/useToast'
import {
  archiveOverview,
  customerProfileRoute,
  errorMessage,
  formatDay,
  money,
  rememberBranch,
} from '@/composables/archiveFlowApi'

const toast = useToast()
const router = useRouter()
const loading = ref(false)
const branches = ref([])
const popup = ref(null)

/** Each card: its count key, the per-branch list it opens, and where «الذهاب» leads. */
const TILES = [
  { key: 'atArchiveItems', list: 'atArchive', kind: 'bundle', title: 'أسماء بانتظار استلامك', icon: 'tabler-inbox', color: 'warning', to: '/archive-sales', go: 'أرشيف المبيعات' },
  { key: 'newRequests', list: 'newRequests', kind: 'request', title: 'طلبات متابعين جديدة', icon: 'tabler-user-question', color: 'warning', to: '/archive-requests', go: 'طلبات المتابعين' },
  { key: 'atCommitteeItems', list: 'atCommittee', kind: 'bundle', title: 'أسماء عند اللجان', icon: 'tabler-users-group', color: 'info', to: '/archive-sales', go: 'أرشيف المبيعات' },
  { key: 'requestsAtCommittee', list: 'requestsAtCommittee', kind: 'request', title: 'طلبات عند اللجان', icon: 'tabler-send', color: 'info', to: '/archive-requests', go: 'طلبات المتابعين' },
  { key: 'readyBundles', list: 'ready', kind: 'bundle', title: 'حزم لم يرسلها الموظفون', icon: 'tabler-clock', color: 'secondary', to: '/archive-sales', go: 'أرشيف المبيعات' },
  { key: 'needsFixBundles', list: 'needsFix', kind: 'bundle', title: 'حزم أُعيدت للموظفين', icon: 'tabler-arrow-back-up', color: 'error', to: '/archive-sales', go: 'أرشيف المبيعات' },
  { key: 'requestsWithFollower', list: 'requestsWithFollower', kind: 'request', title: 'بانتظار تأكيد المتابع', icon: 'tabler-hourglass', color: 'secondary', to: '/archive-requests', go: 'طلبات المتابعين' },
  { key: 'approvedBundles', list: 'approved', kind: 'bundle', title: 'حزم مؤرشفة بالكامل', icon: 'tabler-archive', color: 'success', to: '/archive-sales', go: 'أرشيف المبيعات' },
]

const okBranches = computed(() => branches.value.filter(b => b.ok && b.data))
const failed = computed(() => branches.value.filter(b => !b.ok))

const totals = computed(() => {
  const sum = Object.fromEntries(TILES.map(t => [t.key, 0]))
  for (const b of okBranches.value) {
    for (const t of TILES)
      sum[t.key] += Number(b.data.counts?.[t.key] || 0)
  }

  return sum
})

/** Branches with something waiting on the archive manager, busiest first. */
const actionBranches = computed(() => okBranches.value
  .map(b => ({
    cityValue: b.cityValue,
    cityName: b.cityName,
    bundles: b.data.atArchive || [],
    requests: b.data.newRequests || [],
  }))
  .filter(b => b.bundles.length || b.requests.length)
  .sort((a, b) => (b.bundles.length + b.requests.length) - (a.bundles.length + a.requests.length)))

const branchRows = computed(() => okBranches.value
  .map(b => ({ cityValue: b.cityValue, cityName: b.cityName, ...b.data.counts }))
  .sort((a, b) => (b.atArchiveItems + b.newRequests) - (a.atArchiveItems + a.newRequests)))

/** Popup content: the card's list grouped by branch. */
const popupGroups = computed(() => {
  const t = popup.value
  if (!t)
    return []

  return okBranches.value
    .map(b => ({ cityValue: b.cityValue, cityName: b.cityName, rows: b.data[t.list] || [] }))
    .filter(g => g.rows.length)
    .sort((a, b) => b.rows.length - a.rows.length)
})

async function load() {
  loading.value = true
  try {
    const data = await archiveOverview()
    branches.value = data?.branches || []
  }
  catch (e) {
    toast.error(errorMessage(e, 'تعذر تحميل النظرة العامة'))
  }
  finally {
    loading.value = false
  }
}

function go(path, cityValue) {
  if (cityValue)
    rememberBranch(cityValue)
  popup.value = null
  router.push(path)
}

function formatDate(raw) {
  const parts = String(raw || '').slice(0, 10).split('-')

  return parts.length === 3 ? `${parts[2]}/${parts[1]}` : ''
}

function bundleDetail(list, b) {
  switch (list) {
  case 'atArchive': return `بانتظارك ${b.toArchiveCount} من ${b.itemCount}`
  case 'atCommittee': return `عند اللجنة ${b.atCommitteeCount}${b.recipientName ? ` · ${b.recipientName}` : ''}`
  case 'needsFix': return `مرفوض ${b.refusedCount ?? '—'} من ${b.itemCount}`
  default: return `${b.itemCount} اسم`
  }
}

onMounted(load)
</script>

<template>
  <div>
    <div class="d-flex flex-wrap align-center justify-space-between gap-3 mb-4">
      <div>
        <h1 class="text-h5 font-weight-bold mb-1">
          الأرشيف — كل الفروع
        </h1>
        <p class="text-body-2 text-medium-emphasis mb-0">
          ما ينتظرك الآن، وحالة الأرشيف في كل فرع
        </p>
      </div>
      <VBtn
        icon="tabler-refresh"
        variant="tonal"
        :loading="loading"
        @click="load"
      />
    </div>

    <VAlert
      v-if="failed.length"
      type="warning"
      variant="tonal"
      class="mb-4"
    >
      تعذر تحميل: {{ failed.map(b => b.cityName).join('، ') }}
    </VAlert>

    <div
      v-if="loading && !branches.length"
      class="text-center py-10"
    >
      <VProgressCircular
        indeterminate
        color="primary"
      />
    </div>

    <template v-else>
      <VRow class="mb-2">
        <VCol
          v-for="t in TILES"
          :key="t.key"
          cols="6"
          md="3"
        >
          <VCard
            class="h-100"
            @click="popup = t"
          >
            <VCardText class="d-flex align-center gap-3">
              <VAvatar
                :color="totals[t.key] ? t.color : 'secondary'"
                variant="tonal"
                rounded
              >
                <VIcon :icon="t.icon" />
              </VAvatar>
              <div>
                <div class="text-h5 font-weight-bold">
                  {{ totals[t.key].toLocaleString('en-US') }}
                </div>
                <div class="text-body-2 text-medium-emphasis">
                  {{ t.title }}
                </div>
              </div>
            </VCardText>
          </VCard>
        </VCol>
      </VRow>

      <VCard class="mb-4">
        <VCardTitle class="pt-4">
          يحتاج إجراءك
        </VCardTitle>
        <VCardText>
          <div
            v-if="!actionBranches.length"
            class="text-medium-emphasis py-4 text-center"
          >
            لا يوجد شيء بانتظارك الآن
          </div>
          <VExpansionPanels
            v-else
            multiple
            variant="accordion"
          >
            <VExpansionPanel
              v-for="b in actionBranches"
              :key="b.cityValue"
            >
              <VExpansionPanelTitle>
                <span class="font-weight-bold me-3">{{ b.cityName }}</span>
                <VChip
                  v-if="b.bundles.length"
                  size="small"
                  color="warning"
                  class="me-2"
                >
                  {{ b.bundles.length }} حزمة للاستلام
                </VChip>
                <VChip
                  v-if="b.requests.length"
                  size="small"
                  color="warning"
                  variant="outlined"
                >
                  {{ b.requests.length }} طلب جديد
                </VChip>
              </VExpansionPanelTitle>
              <VExpansionPanelText>
                <div
                  v-for="x in b.bundles"
                  :key="`b-${x.bundleId}`"
                  class="row-line"
                >
                  <VIcon
                    icon="tabler-inbox"
                    color="warning"
                  />
                  <div class="flex-grow-1 min-w-0">
                    <div class="font-weight-bold">
                      {{ x.toArchiveCount }} اسم من {{ x.employeeName || 'موظف مبيعات' }}
                    </div>
                    <div class="text-body-2 text-medium-emphasis">
                      حزمة {{ x.bundleNumber }}
                      <span v-if="x.recipientName"> · سلّمها {{ x.recipientName }}</span>
                    </div>
                  </div>
                  <VBtn
                    color="primary"
                    variant="tonal"
                    size="small"
                    @click="go('/archive-sales', b.cityValue)"
                  >
                    استلام
                  </VBtn>
                </div>
                <div
                  v-for="r in b.requests"
                  :key="`r-${r.id}`"
                  class="row-line"
                >
                  <VIcon
                    icon="tabler-user-question"
                    color="warning"
                  />
                  <div class="flex-grow-1 min-w-0">
                    <RouterLink
                      :to="customerProfileRoute(b.cityValue, r)"
                      class="font-weight-bold"
                    >
                      {{ r.customerName || `زبون ${r.customerId}` }}
                    </RouterLink>
                    <VChip
                      size="x-small"
                      class="ms-2"
                      :color="r.remaining > 0 ? 'error' : 'success'"
                    >
                      {{ r.remaining > 0 ? `المتبقي ${money(r.remaining)}` : 'منتهٍ' }}
                    </VChip>
                    <div class="text-body-2 text-medium-emphasis">
                      المتابع {{ r.followerName || '—' }} · {{ formatDay(r.requestedAtUtc) }}
                    </div>
                    <div
                      v-if="r.followerNote"
                      class="text-body-2"
                    >
                      ملاحظة المتابع: {{ r.followerNote }}
                    </div>
                  </div>
                  <VBtn
                    color="primary"
                    variant="tonal"
                    size="small"
                    @click="go('/archive-requests', b.cityValue)"
                  >
                    إسناد
                  </VBtn>
                </div>
              </VExpansionPanelText>
            </VExpansionPanel>
          </VExpansionPanels>
        </VCardText>
      </VCard>

      <VCard>
        <VCardTitle class="pt-4">
          الفروع
        </VCardTitle>
        <VTable density="comfortable">
          <thead>
            <tr>
              <th>الفرع</th>
              <th>بانتظارك</th>
              <th>طلبات جديدة</th>
              <th>عند اللجنة</th>
              <th>لم تُرسل</th>
              <th>أُعيدت</th>
              <th />
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="b in branchRows"
              :key="b.cityValue"
            >
              <td class="font-weight-medium">
                {{ b.cityName }}
              </td>
              <td :class="{ 'text-warning font-weight-bold': b.atArchiveItems }">
                {{ b.atArchiveItems }}
              </td>
              <td :class="{ 'text-warning font-weight-bold': b.newRequests }">
                {{ b.newRequests }}
              </td>
              <td>{{ b.atCommitteeItems }}</td>
              <td>{{ b.readyBundles }}</td>
              <td :class="{ 'text-error': b.needsFixBundles }">
                {{ b.needsFixBundles }}
              </td>
              <td>
                <VBtn
                  size="small"
                  variant="text"
                  icon="tabler-chevron-left"
                  @click="go('/archive-sales', b.cityValue)"
                />
              </td>
            </tr>
          </tbody>
        </VTable>
      </VCard>
    </template>

    <VDialog
      :model-value="!!popup"
      max-width="760"
      scrollable
      @update:model-value="v => { if (!v) popup = null }"
    >
      <VCard v-if="popup">
        <VCardTitle class="d-flex align-center justify-space-between pt-4">
          <span>{{ popup.title }} — {{ totals[popup.key].toLocaleString('en-US') }}</span>
          <VBtn
            icon="tabler-x"
            variant="text"
            @click="popup = null"
          />
        </VCardTitle>
        <VDivider />
        <VCardText>
          <div
            v-if="!popupGroups.length"
            class="text-medium-emphasis text-center py-6"
          >
            لا يوجد شيء هنا
          </div>
          <VExpansionPanels
            v-else
            multiple
            variant="accordion"
          >
            <VExpansionPanel
              v-for="g in popupGroups"
              :key="g.cityValue"
            >
              <VExpansionPanelTitle>
                <span class="font-weight-bold me-3">{{ g.cityName }}</span>
                <VChip size="small">
                  {{ g.rows.length }}
                </VChip>
              </VExpansionPanelTitle>
              <VExpansionPanelText>
                <template v-if="popup.kind === 'bundle'">
                  <div
                    v-for="x in g.rows"
                    :key="x.bundleId"
                    class="row-line"
                  >
                    <div class="flex-grow-1 min-w-0">
                      <div class="font-weight-medium">
                        {{ x.employeeName || 'موظف مبيعات' }} · حزمة {{ x.bundleNumber }}
                      </div>
                      <div class="text-body-2 text-medium-emphasis">
                        {{ formatDate(x.periodStart) }} — {{ formatDate(x.periodEnd) }} · {{ bundleDetail(popup.list, x) }}
                      </div>
                    </div>
                  </div>
                </template>
                <template v-else>
                  <div
                    v-for="r in g.rows"
                    :key="r.id"
                    class="row-line"
                  >
                    <div class="flex-grow-1 min-w-0">
                      <RouterLink
                        :to="customerProfileRoute(g.cityValue, r)"
                        class="font-weight-medium"
                        @click="popup = null"
                      >
                        {{ r.customerName || `زبون ${r.customerId}` }}
                      </RouterLink>
                      <VChip
                        size="x-small"
                        class="ms-2"
                        :color="r.remaining > 0 ? 'error' : 'success'"
                      >
                        {{ r.remaining > 0 ? `المتبقي ${money(r.remaining)}` : 'منتهٍ' }}
                      </VChip>
                      <div class="text-body-2 text-medium-emphasis">
                        المتابع {{ r.followerName || '—' }}
                        <span v-if="r.committeeUser"> · اللجنة: {{ r.committeeUser }}</span>
                        · {{ formatDay(r.requestedAtUtc) }}
                      </div>
                      <div
                        v-if="r.followerNote"
                        class="text-body-2"
                      >
                        ملاحظة المتابع: {{ r.followerNote }}
                      </div>
                    </div>
                  </div>
                </template>
              </VExpansionPanelText>
            </VExpansionPanel>
          </VExpansionPanels>
        </VCardText>
        <VDivider />
        <VCardActions>
          <VSpacer />
          <VBtn
            variant="text"
            @click="popup = null"
          >
            إغلاق
          </VBtn>
          <VBtn
            color="primary"
            variant="elevated"
            append-icon="tabler-chevron-left"
            @click="go(popup.to, popupGroups.length === 1 ? popupGroups[0].cityValue : null)"
          >
            الذهاب إلى {{ popup.go }}
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>
  </div>
</template>

<style scoped>
.row-line {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 10px 4px;
  border-block-end: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}

.row-line:last-child {
  border-block-end: none;
}

.min-w-0 {
  min-inline-size: 0;
}
</style>
