<script setup>
import { computed, ref, watch } from 'vue'
import { useToast } from '@/composables/useToast'
import ArchiveBranchSelect from '@/components/ArchiveBranchSelect.vue'
import {
  REQUEST_STATUS,
  archiveFinishedCustomers,
  errorMessage,
  formatDay,
} from '@/composables/archiveFlowApi'

const toast = useToast()
const city = ref('')
const loading = ref(false)
const groups = ref([])
const search = ref('')
const onlyNotRequested = ref(false)

const totals = computed(() => ({
  customers: groups.value.reduce((s, g) => s + (g.total || 0), 0),
  requested: groups.value.reduce((s, g) => s + (g.requested || 0), 0),
}))

const visibleGroups = computed(() => {
  const q = search.value.trim()

  return groups.value
    .map(g => {
      const customers = (g.customers || []).filter(c =>
        (!onlyNotRequested.value || !c.requestStatus)
        && (!q || String(c.customerName || '').includes(q) || String(g.followerName || '').includes(q)))

      return { ...g, customers }
    })
    .filter(g => g.customers.length)
})

async function load() {
  if (!city.value)
    return
  loading.value = true
  groups.value = []
  try {
    groups.value = await archiveFinishedCustomers(city.value, 30) || []
  }
  catch (e) {
    toast.error(errorMessage(e, 'تعذر تحميل الزبائن المنتهين'))
  }
  finally {
    loading.value = false
  }
}

watch(city, load)
</script>

<template>
  <div>
    <div class="d-flex flex-wrap align-center justify-space-between gap-3 mb-4">
      <div>
        <h1 class="text-h5 font-weight-bold mb-1">
          الزبائن المنتهون — آخر 30 يوماً
        </h1>
        <p class="text-body-2 text-medium-emphasis mb-0">
          {{ totals.customers }} زبون · طلب المتابعون أرشيف {{ totals.requested }} منهم
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

    <div class="d-flex flex-wrap align-center gap-4 mb-4">
      <VTextField
        v-model="search"
        prepend-inner-icon="tabler-search"
        placeholder="بحث باسم الزبون أو المتابع"
        density="compact"
        hide-details
        clearable
        style="max-inline-size: 360px;"
      />
      <VSwitch
        v-model="onlyNotRequested"
        color="primary"
        hide-details
        label="غير المطلوبة فقط"
      />
    </div>

    <div
      v-if="loading && !groups.length"
      class="text-center py-10"
    >
      <VProgressCircular
        indeterminate
        color="primary"
      />
    </div>

    <VCard
      v-else-if="!visibleGroups.length"
      class="text-center text-medium-emphasis py-10"
    >
      <VIcon
        icon="tabler-user-check"
        size="40"
        class="mb-2"
      />
      <div>لا يوجد زبائن منتهون خلال آخر 30 يوماً</div>
    </VCard>

    <VExpansionPanels
      v-else
      multiple
    >
      <VExpansionPanel
        v-for="g in visibleGroups"
        :key="g.followerUserId"
      >
        <VExpansionPanelTitle>
          <div class="d-flex align-center gap-3 flex-grow-1">
            <VAvatar
              color="primary"
              variant="tonal"
              size="34"
            >
              <VIcon icon="tabler-user" />
            </VAvatar>
            <span class="font-weight-bold">{{ g.followerName || `متابع ${g.followerUserId}` }}</span>
            <VSpacer />
            <VChip
              size="small"
              :color="g.requested >= g.total ? 'success' : 'warning'"
              class="me-2"
            >
              طُلب {{ g.requested }} من {{ g.total }}
            </VChip>
          </div>
        </VExpansionPanelTitle>
        <VExpansionPanelText>
          <div
            v-for="c in g.customers"
            :key="c.customerId"
            class="customer-row"
          >
            <div class="flex-grow-1">
              <div class="font-weight-medium text-high-emphasis">
                {{ c.customerName }}
              </div>
              <div class="text-caption text-medium-emphasis">
                انتهى {{ formatDay(c.finishedAt) }}
                <span v-if="c.listName"> · {{ c.listName }}</span>
                <span v-if="c.phoneNumber"> · {{ c.phoneNumber }}</span>
              </div>
            </div>
            <VChip
              size="small"
              variant="tonal"
              :color="c.requestStatus ? REQUEST_STATUS[c.requestStatus]?.color : 'default'"
            >
              {{ c.requestStatus ? (REQUEST_STATUS[c.requestStatus]?.label || c.requestStatus) : 'لم يُطلب' }}
            </VChip>
          </div>
        </VExpansionPanelText>
      </VExpansionPanel>
    </VExpansionPanels>
  </div>
</template>

<style scoped>
.customer-row {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 10px 0;
  border-block-end: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}

.customer-row:last-child {
  border-block-end: none;
}
</style>
