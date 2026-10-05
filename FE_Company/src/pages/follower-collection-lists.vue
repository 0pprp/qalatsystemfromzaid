<script setup>
import { getAuthHeaders } from '@/services/tokenService'
import { useToast } from '@/composables/useToast'
import { useUserRole } from '@/composables/useUserRole'
import axios from 'axios'
import { onMounted, ref } from 'vue'

const apiUrl = localStorage.getItem('LinkCity')
const toast = useToast()
const { isAdmin } = useUserRole()

const loading = ref(false)
const savingId = ref(0)
const rows = ref([])
const filter = ref('')

const headers = [
  { title: 'القائمة', key: 'delegateName' },
  { title: 'عدد الزبائن', key: 'numberOfCustomer' },
  { title: 'غير مصفرين', key: 'numberOfCustomerIsNotZero' },
  { title: 'جباية المتابعين', key: 'followerCollectionEnabled', sortable: false },
]

async function load() {
  if (!isAdmin.value)
    return
  loading.value = true
  try {
    const { data } = await axios.get(`${apiUrl}Delegates/FollowerCollectionLists`, {
      headers: getAuthHeaders(),
    })
    rows.value = Array.isArray(data) ? data : []
  }
  catch (err) {
    toast.error(err?.response?.data?.message || 'تعذر تحميل القوائم')
  }
  finally {
    loading.value = false
  }
}

function matches(row) {
  const q = filter.value.trim()
  if (!q)
    return true
  return String(row.delegateName || row.DelegateName || '').includes(q)
}

async function toggle(row, enabled) {
  const id = Number(row.delegateId ?? row.DelegateId ?? 0)
  if (!id || !isAdmin.value)
    return
  savingId.value = id
  const prev = !!(row.followerCollectionEnabled ?? row.FollowerCollectionEnabled)
  row.followerCollectionEnabled = enabled
  try {
    await axios.put(
      `${apiUrl}Delegates/FollowerCollectionEnabled/${id}`,
      { enabled },
      { headers: getAuthHeaders() },
    )
    toast.success(enabled
      ? 'تم تفعيل جباية المتابعين — جباية المندوب مقفلة على هذه القائمة'
      : 'تم إرجاع الجباية للمندوب')
  }
  catch (err) {
    row.followerCollectionEnabled = prev
    toast.error(err?.response?.data?.message || 'تعذر حفظ الحالة')
  }
  finally {
    savingId.value = 0
  }
}

onMounted(load)
</script>

<template>
  <div>
    <VCard class="pa-6 mb-4">
      <div class="d-flex flex-wrap align-center justify-space-between ga-3">
        <div>
          <h2 class="text-h5 mb-1">
            جباية من قبل المتابعين
          </h2>
          <p class="text-medium-emphasis mb-0">
            فعّل القائمة ليصير التسديد من تطبيق المتابع، ويقفل تسديد المندوب على نفس القائمة من السيرفر فوراً.
          </p>
        </div>
        <VBtn
          color="primary"
          prepend-icon="tabler-refresh"
          :loading="loading"
          @click="load"
        >
          تحديث
        </VBtn>
      </div>
    </VCard>

    <VAlert
      v-if="!isAdmin"
      type="warning"
      variant="tonal"
      class="mb-4"
    >
      هذا التبويب لصلاحية المحاسب الرئيسي فقط.
    </VAlert>

    <VCard
      v-else
      class="pa-4"
    >
      <VTextField
        v-model="filter"
        class="mb-4"
        density="comfortable"
        variant="outlined"
        hide-details
        prepend-inner-icon="tabler-search"
        placeholder="بحث باسم القائمة…"
      />
      <VDataTable
        :headers="headers"
        :items="rows.filter(matches)"
        :loading="loading"
        :items-per-page="50"
        class="text-no-wrap"
        items-per-page-text="عدد السجلات"
      >
        <template #item.delegateName="{ item }">
          <strong>{{ item.delegateName || item.DelegateName }}</strong>
        </template>
        <template #item.numberOfCustomer="{ item }">
          {{ item.numberOfCustomer ?? item.NumberOfCustomer ?? 0 }}
        </template>
        <template #item.numberOfCustomerIsNotZero="{ item }">
          {{ item.numberOfCustomerIsNotZero ?? item.NumberOfCustomerIsNotZero ?? 0 }}
        </template>
        <template #item.followerCollectionEnabled="{ item }">
          <div class="d-flex align-center ga-3">
            <VSwitch
              :model-value="!!(item.followerCollectionEnabled ?? item.FollowerCollectionEnabled)"
              :loading="savingId === Number(item.delegateId ?? item.DelegateId)"
              :disabled="savingId === Number(item.delegateId ?? item.DelegateId)"
              color="success"
              hide-details
              @update:model-value="v => toggle(item, v)"
            />
            <VChip
              size="small"
              :color="(item.followerCollectionEnabled ?? item.FollowerCollectionEnabled) ? 'success' : 'default'"
              label
            >
              {{ (item.followerCollectionEnabled ?? item.FollowerCollectionEnabled) ? 'جباية متابع' : 'جباية مندوب' }}
            </VChip>
          </div>
        </template>
      </VDataTable>
    </VCard>
  </div>
</template>
