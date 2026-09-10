<script setup>
import { getAuthHeaders } from '@/services/tokenService'
import axios from 'axios'
import { onMounted, ref } from 'vue'
import { useToast } from '@/composables/useToast'

const toast = useToast()
const apiUrl = localStorage.getItem('LinkCity')
const q = ref('')
const users = ref([])
const followers = ref([])
const loading = ref(false)
const enablingId = ref(null)

async function loadUsers() {
  loading.value = true
  try {
    const { data } = await axios.get(
      `${apiUrl}follower-admin/users`,
      { params: { q: q.value || undefined }, headers: getAuthHeaders() },
    )
    users.value = Array.isArray(data) ? data : []
  } catch (e) {
    toast.error('تعذر تحميل المستخدمين')
  } finally {
    loading.value = false
  }
}

async function loadFollowers() {
  try {
    const { data } = await axios.get(
      `${apiUrl}follower-admin/followers`,
      { headers: getAuthHeaders() },
    )
    followers.value = Array.isArray(data) ? data : []
  } catch (e) {
    toast.error('تعذر تحميل المتابعين')
  }
}

async function enableFollower(row) {
  enablingId.value = row.userId
  try {
    await axios.post(
      `${apiUrl}follower-admin/enable`,
      {
        userId: row.userId,
        cityId: row.cityId || null,
        cityName: row.cityName || null,
      },
      { headers: getAuthHeaders() },
    )
    toast.success(`تم تفعيل المتابع: ${row.userName}`)
    await Promise.all([loadUsers(), loadFollowers()])
  } catch (e) {
    toast.error(e?.response?.data?.message || 'فشل التفعيل')
  } finally {
    enablingId.value = null
  }
}

async function disableFollower(userId) {
  try {
    await axios.post(
      `${apiUrl}follower-admin/disable/${userId}`,
      {},
      { headers: getAuthHeaders() },
    )
    toast.success('تم تعطيل صلاحية المتابع')
    await Promise.all([loadUsers(), loadFollowers()])
  } catch (e) {
    toast.error(e?.response?.data?.message || 'فشل التعطيل')
  }
}

onMounted(async () => {
  await Promise.all([loadUsers(), loadFollowers()])
})
</script>

<template>
  <VCard class="pa-6">
    <div class="d-flex flex-wrap align-center justify-space-between gap-3 mb-4">
      <div>
        <h2 class="text-h5 mb-1">
          إدارة المتابعين
        </h2>
        <p class="text-medium-emphasis mb-0">
          المتابع هو مستخدم من نظام Users — وليس مندوب جباية.
        </p>
      </div>
      <VBtn
        color="primary"
        prepend-icon="tabler-refresh"
        :loading="loading"
        @click="loadUsers(); loadFollowers()"
      >
        تحديث
      </VBtn>
    </div>

    <VRow class="mb-6">
      <VCol
        cols="12"
        md="6"
      >
        <VTextField
          v-model="q"
          label="بحث بالاسم"
          prepend-inner-icon="tabler-search"
          clearable
          @keyup.enter="loadUsers"
        />
      </VCol>
      <VCol
        cols="12"
        md="3"
        class="d-flex align-center"
      >
        <VBtn
          color="primary"
          variant="tonal"
          @click="loadUsers"
        >
          بحث
        </VBtn>
      </VCol>
    </VRow>

    <VTable class="mb-8">
      <thead>
        <tr>
          <th>المستخدم</th>
          <th>النوع</th>
          <th>المحافظة</th>
          <th>متابع؟</th>
          <th />
        </tr>
      </thead>
      <tbody>
        <tr
          v-for="row in users"
          :key="row.userId"
        >
          <td>{{ row.userName }}</td>
          <td>{{ row.userType || '—' }}</td>
          <td>{{ row.cityName || '—' }}</td>
          <td>
            <VChip
              size="small"
              :color="row.isFollower ? 'success' : 'default'"
            >
              {{ row.isFollower ? 'مفعّل' : 'غير مفعّل' }}
            </VChip>
          </td>
          <td class="text-end">
            <VBtn
              v-if="!row.isFollower"
              size="small"
              color="primary"
              :loading="enablingId === row.userId"
              @click="enableFollower(row)"
            >
              تفعيل متابع
            </VBtn>
            <VBtn
              v-else
              size="small"
              color="error"
              variant="tonal"
              @click="disableFollower(row.userId)"
            >
              تعطيل
            </VBtn>
          </td>
        </tr>
      </tbody>
    </VTable>

    <h3 class="text-h6 mb-3">
      المتابعون المفعّلون
    </h3>
    <VTable>
      <thead>
        <tr>
          <th>الاسم</th>
          <th>المحافظة</th>
          <th>الحالة</th>
        </tr>
      </thead>
      <tbody>
        <tr
          v-for="f in followers.filter(x => x.isActive)"
          :key="f.userId"
        >
          <td>{{ f.userName }}</td>
          <td>{{ f.cityName || '—' }}</td>
          <td>
            <VChip
              size="small"
              color="success"
            >
              نشط
            </VChip>
          </td>
        </tr>
      </tbody>
    </VTable>
  </VCard>
</template>
