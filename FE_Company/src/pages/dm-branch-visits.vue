<script setup>
import { onMounted, ref } from 'vue'
import { dmBranchVisits } from '@/composables/delegatedManagerApi'
import { useToast } from '@/composables/useToast'

defineOptions({ name: 'DmBranchVisits' })
definePage({ meta: { layout: 'dm' } })

const toast = useToast()
const busy = ref(false)
const rows = ref([])

function pick(obj, ...keys) {
  if (!obj) return undefined
  for (const k of keys) {
    if (obj[k] != null && obj[k] !== '')
      return obj[k]
  }
  return undefined
}

function fmt(v) {
  if (!v) return '—'
  const d = new Date(v)
  if (Number.isNaN(d.getTime())) return String(v)
  return d.toLocaleString('en-GB', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  })
}

function membersLabel(row) {
  const list = pick(row, 'members', 'Members') || []
  if (!Array.isArray(list)) return '—'
  return list
    .map(m => pick(m, 'displayName', 'DisplayName', 'userName', 'UserName') || '')
    .filter(Boolean)
    .join('، ') || '—'
}

function ratings(row) {
  const list = pick(row, 'delegateRatings', 'DelegateRatings') || []
  return Array.isArray(list) ? list : []
}

async function load() {
  busy.value = true
  try {
    const data = await dmBranchVisits({ take: 100 })
    rows.value = Array.isArray(data) ? data : (pick(data, 'items', 'Items') || [])
  }
  catch (e) {
    rows.value = []
    toast.error(e?.response?.data?.message || e?.message || 'تعذر تحميل الزيارات')
  }
  finally {
    busy.value = false
  }
}

onMounted(load)
</script>

<template>
  <div>
    <div class="d-flex flex-wrap align-center justify-space-between gap-2 mb-4">
      <h1 class="text-h5 mb-0">
        أعمال إدارة الفرع
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
      v-if="!busy && !rows.length"
      type="info"
      variant="tonal"
    >
      لا توجد زيارات مسجّلة
    </VAlert>

    <VCard
      v-for="row in rows"
      :key="pick(row, 'id', 'Id')"
      variant="outlined"
      class="mb-3"
    >
      <VCardText>
        <div class="d-flex justify-space-between gap-2 flex-wrap mb-2">
          <strong>{{ pick(row, 'cityName', 'CityName', 'cityValue', 'CityValue') || 'محافظة' }}</strong>
          <VChip
            size="small"
            variant="tonal"
            :color="String(pick(row, 'status', 'Status')) === 'Open' ? 'success' : 'secondary'"
          >
            {{ String(pick(row, 'status', 'Status')) === 'Open' ? 'مفتوحة' : 'مغلقة' }}
          </VChip>
        </div>
        <div class="text-caption text-medium-emphasis mb-2">
          {{ pick(row, 'openedByDisplayName', 'OpenedByDisplayName') || '—' }}
        </div>
        <div class="text-body-2">
          الوصول: {{ fmt(pick(row, 'arrivedAtUtc', 'ArrivedAtUtc')) }}
        </div>
        <div class="text-body-2">
          المغادرة: {{ fmt(pick(row, 'departedAtUtc', 'DepartedAtUtc')) }}
        </div>
        <div class="text-body-2 mt-1">
          الأعضاء: {{ membersLabel(row) }}
        </div>
        <div
          v-for="(r, i) in ratings(row)"
          :key="i"
          class="text-body-2"
        >
          {{ pick(r, 'delegateName', 'DelegateName') }}: {{ pick(r, 'score', 'Score') }}/10
        </div>
        <div
          v-if="pick(row, 'notes', 'Notes')"
          class="text-body-2 mt-2"
        >
          {{ pick(row, 'notes', 'Notes') }}
        </div>
      </VCardText>
    </VCard>
  </div>
</template>
