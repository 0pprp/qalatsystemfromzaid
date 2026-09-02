<script setup>
import { onMounted, ref } from 'vue'
import { requestStatusLabel, smGet } from '@/composables/salesManagerApi'

const rows = ref([])
const status = ref('')

async function load() {
  rows.value = await smGet(`sales-requests${status.value ? `?status=${status.value}` : ''}`)
}

onMounted(load)
</script>

<template>
  <div>
    <div class="d-flex justify-space-between mb-4">
      <h4>طلبات المبيعات</h4>
      <VBtn
        color="primary"
        :to="{ name: 'sales-manager-request-create' }"
      >
        + طلب مبيع جديد
      </VBtn>
    </div>
    <VChipGroup
      v-model="status"
      column
      @update:model-value="load"
    >
      <VChip
        value=""
        filter
      >
        الكل
      </VChip>
      <VChip
        value="New"
        filter
      >
        جديد
      </VChip>
      <VChip
        value="InProgress"
        filter
      >
        قيد المعالجة
      </VChip>
      <VChip
        value="ConvertedToSale"
        filter
      >
        تحول إلى بيع
      </VChip>
      <VChip
        value="Completed"
        filter
      >
        مكتمل
      </VChip>
      <VChip
        value="Rejected"
        filter
      >
        مرفوض
      </VChip>
    </VChipGroup>
    <VRow class="mt-4">
      <VCol
        v-for="row in rows"
        :key="row.id"
        cols="12"
        md="6"
      >
        <VCard>
          <VCardText>
            <div>طلب #{{ row.id }}</div>
            <strong>{{ row.customerName }}</strong>
            <div>{{ row.targetEmployeeName }} — {{ row.cityName }}</div>
            <div>{{ requestStatusLabel[row.status] || row.status }}</div>
            <div class="text-medium-emphasis">
              {{ row.createdAtUtc }}
            </div>
          </VCardText>
        </VCard>
      </VCol>
    </VRow>
  </div>
</template>
