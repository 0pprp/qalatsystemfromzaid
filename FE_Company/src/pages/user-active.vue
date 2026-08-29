<script setup>
import { getAuthHeaders } from '@/services/tokenService'
import AppTextField from '@core/components/app-form-elements/AppTextField.vue'
import axios from 'axios'
import { computed, ref } from 'vue'

import ModernStatCard from "@/components/ModernStatCard.vue"


// رابط الـ API
const apiUrl = localStorage.getItem('LinkCity')

// بيانات النشاطات
const activitiesData = ref([])

// حالة التحميل
const loading = ref(false)

// فلاتر البحث: من التاريخ وإلى التاريخ
const filters = ref({
  fromDate: '',  // يجب تعبئتها بصيغة yyyy-MM-dd
  toDate: '',    // يجب تعبئتها بصيغة yyyy-MM-dd
})

// دالة تنسيق التاريخ
const formattedDate = date =>
  date ? new Date(date).toLocaleDateString('en-CA') : 'لا يوجد'

// تعريف أعمدة جدول النشاطات
const headers = [
  { title: 'رقم النشاط', key: 'activityID' },
  { title: 'وصف النشاط', key: 'activityDescription' },
  { title: 'التاريخ', key: 'activityDate' },
  { title: 'اسم المستخدم', key: 'userName' },
]

// دالة جلب بيانات النشاطات بناءً على فلاتر التاريخ
async function fetchActivitiesData() {
  try {
    loading.value = true

    const authHeader = getAuthHeaders()

    const fromDate = filters.value.fromDate || 'null'
    const toDate = filters.value.toDate || 'null'

    const response = await axios.get(`${apiUrl}Users/Activities_GetByDate/${fromDate}&&${toDate}`, { headers: authHeader })

    activitiesData.value = response.data
  } catch (error) {
    console.error(error)
  } finally {
    loading.value = false
  }
}

// إجماليات النشاطات
const totals = computed(() => [
  {
    icon: 'tabler-activity',  // أيقونة تمثل النشاطات
    value: activitiesData.value.length,
    title: 'عدد النشاطات الكلي',
    color: "primary",
    gradient: "linear-gradient(135deg, #8E2DE2 0%, #4A00E0 100%)",
  },
])

function handleDateChangeFromDate(event) {
  const rawDate = event.target.value

  filters.value.fromDate = new Date(rawDate).toLocaleDateString('en-CA')
}

function handleDateChangeToDate(event) {
  const rawDate = event.target.value

  filters.value.toDate = new Date(rawDate).toLocaleDateString('en-CA')
}
</script>

<template>
  <!-- عرض الإجماليات -->
  <VRow class="stats-row mb-6">
    <VCol
      v-for="(card, index) in totals"
      :key="index"
      cols="12"
      sm="6"
      md="3"
    >
      <ModernStatCard
        :title="card.title"
        :value="card.value"
        :icon="card.icon"
        :color="card.color"
        :gradient="card.gradient"
      />
    </VCol>
  </VRow>
  <VCard class="pa-10">
    <VForm>
      <!-- فلاتر البحث: من التاريخ وإلى التاريخ -->
      <VRow>
        <VCol
          md="2"
          cols="12"
        >
          <VLabel class="mb-2">
            من التاريخ
          </VLabel>
          <AppTextField
            v-model="filters.fromDate"
            type="date"
            prepend-inner-icon="tabler-calendar"
            @input="handleDateChangeFromDate"
          />
        </VCol>
        <VCol
          md="2"
          cols="12"
        >
          <VLabel class="mb-2">
            إلى التاريخ
          </VLabel>
          <AppTextField
            v-model="filters.toDate"
            type="date"
            prepend-inner-icon="tabler-calendar"
            @input="handleDateChangeToDate"
          />
        </VCol>
        <VRow style="margin-block-start: 31px;margin-inline: 5px 10px;">
          <VBtn
            color="primary"
            :loading="loading"
            style="margin-block-start: 12px;margin-inline-start: 20px;"
            :disabled="loading"
            prepend-icon="tabler-search"
            @click="fetchActivitiesData"
          >
            بحث
          </VBtn>
        </VRow>
      </VRow>

      <!-- عرض بيانات النشاطات -->
      <VRow>
        <VDataTable
          style="white-space: nowrap;"
          :headers="headers"
          :items="activitiesData"
          :items-per-page="50"
          items-per-page-text="عدد السجل"
          class="custom-data-table"
        >
          <template #item.ActivityDate="{ item }">
            <div style="inline-size: 200px;">
              {{ item.ActivityDate ? formattedDate(item.ActivityDate) : 'لا يوجد' }}
            </div>
          </template>
        </VDataTable>
      </VRow>
    </VForm>
  </VCard>
</template>

<style scoped>
.v-btn {
  margin-block-start: 10px;
}

.v-btn + .v-btn {
  margin-inline-start: 10px;
}

.text-right {
  text-align: end;
}

.d-flex {
  display: flex;
}

.align-center {
  align-items: center;
}

.justify-end {
  justify-content: flex-end;
}
</style>
