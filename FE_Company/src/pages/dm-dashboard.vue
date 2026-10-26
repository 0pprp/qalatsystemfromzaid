<script setup>
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { dmDashboard } from '@/composables/delegatedManagerApi'
import { useToast } from '@/composables/useToast'

defineOptions({ name: 'DmDashboard' })
definePage({ meta: { layout: 'dm' } })

const toast = useToast()
const router = useRouter()
const busy = ref(false)
const summary = ref(null)

function pick(obj, ...keys) {
  if (!obj) return undefined
  for (const k of keys) {
    if (obj[k] != null && obj[k] !== '')
      return obj[k]
  }
  return undefined
}

function n(v) {
  const x = Number(v)
  return Number.isFinite(x) ? x : 0
}

async function load() {
  busy.value = true
  try {
    const data = await dmDashboard()
    const ex = pick(data, 'exceptions', 'Exceptions') || {}
    summary.value = {
      unreadComplaints: n(pick(data, 'unreadComplaints', 'UnreadComplaints')),
      unreadWrongSales: n(pick(data, 'unreadWrongSales', 'UnreadWrongSales')),
      pending: n(pick(ex, 'pending', 'Pending')),
      approved: n(pick(ex, 'approved', 'Approved')),
      rejected: n(pick(ex, 'rejected', 'Rejected')),
      cancelled: n(pick(ex, 'cancelled', 'Cancelled')),
      total: n(pick(ex, 'total', 'Total')),
    }
  }
  catch (e) {
    toast.error(e?.response?.data?.message || e?.message || 'تعذر تحميل اللوحة')
    summary.value = null
  }
  finally {
    busy.value = false
  }
}

onMounted(load)
</script>

<template>
  <div>
    <div class="d-flex align-center justify-space-between mb-4 flex-wrap gap-2">
      <div>
        <h1 class="text-h5 mb-1">
          الرئيسية
        </h1>
        <p class="text-medium-emphasis mb-0">
          ملخص العمل — نفس بيانات تطبيق المدير المفوض
        </p>
      </div>
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
      class="mb-4"
    />

    <div
      v-if="summary"
      class="dm-cards"
    >
      <button
        type="button"
        class="dm-card"
        @click="router.push('/dm-complaints')"
      >
        <span class="dm-card__label">شكاوى غير مقروءة</span>
        <strong class="dm-card__value">{{ summary.unreadComplaints }}</strong>
      </button>
      <button
        type="button"
        class="dm-card"
        @click="router.push('/dm-exceptions')"
      >
        <span class="dm-card__label">استثناءات معلّقة</span>
        <strong class="dm-card__value">{{ summary.pending }}</strong>
      </button>
      <button
        type="button"
        class="dm-card dm-card--warn"
        @click="router.push('/dm-wrong-sales')"
      >
        <span class="dm-card__label">مبيعات خاطئة غير مقروءة</span>
        <strong class="dm-card__value">{{ summary.unreadWrongSales }}</strong>
      </button>
      <div class="dm-card">
        <span class="dm-card__label">استثناءات موافق عليها</span>
        <strong class="dm-card__value">{{ summary.approved }}</strong>
      </div>
      <div class="dm-card">
        <span class="dm-card__label">استثناءات مرفوضة</span>
        <strong class="dm-card__value">{{ summary.rejected }}</strong>
      </div>
      <div class="dm-card">
        <span class="dm-card__label">إجمالي الاستثناءات</span>
        <strong class="dm-card__value">{{ summary.total }}</strong>
      </div>
    </div>
  </div>
</template>

<style scoped>
.dm-cards {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(180px, 1fr));
  gap: 0.75rem;
}
.dm-card {
  text-align: start;
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-radius: 14px;
  padding: 1rem;
  background: rgb(var(--v-theme-surface));
  color: inherit;
  font: inherit;
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
  min-block-size: 100px;
}
button.dm-card { cursor: pointer; }
button.dm-card:hover { border-color: rgb(var(--v-theme-primary)); }
.dm-card--warn {
  background: rgba(230, 140, 40, 0.08);
  border-color: rgba(230, 140, 40, 0.35);
}
.dm-card__label { font-size: 0.85rem; opacity: 0.75; }
.dm-card__value { font-size: 1.6rem; font-weight: 800; }
</style>
