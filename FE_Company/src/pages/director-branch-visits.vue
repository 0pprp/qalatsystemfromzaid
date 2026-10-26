<script setup>
import { onMounted, onUnmounted, ref } from 'vue'
import { smErrorMessage, smGet } from '@/composables/salesManagerApi'
import { useToast } from '@/composables/useToast'

const toast = useToast()
const loading = ref(false)
const rows = ref([])
let timer = null

function pick(obj, ...keys) {
  if (!obj) return undefined
  for (const key of keys) {
    if (obj[key] != null && obj[key] !== '')
      return obj[key]
  }
  return undefined
}

function fmt(dt) {
  if (!dt) return '—'
  const d = new Date(dt)
  if (Number.isNaN(d.getTime())) return String(dt)
  return d.toLocaleString('ar-IQ', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    hour12: true,
  })
}

async function load() {
  loading.value = true
  try {
    const list = await smGet('branch-visits?take=100')
    rows.value = Array.isArray(list) ? list : []
  }
  catch (err) {
    rows.value = []
    toast.error(smErrorMessage(err, 'تعذر تحميل أعمال إدارة الفرع'))
  }
  finally {
    loading.value = false
  }
}

onMounted(() => {
  load()
  timer = setInterval(load, 30000)
})
onUnmounted(() => {
  if (timer) clearInterval(timer)
})
</script>

<template>
  <div>
    <VCard>
      <VCardTitle class="d-flex align-center justify-space-between flex-wrap gap-2">
        <span>أعمال إدارة الفرع</span>
        <VBtn
          color="primary"
          variant="tonal"
          :loading="loading"
          @click="load"
        >
          تحديث
        </VBtn>
      </VCardTitle>
      <VCardText>
        <p class="text-medium-emphasis mb-4">
          زيارات المحافظات من تطبيق إدارة الفرع — وصول، أعضاء، تقييمات المناديب، وملاحظات المغادرة.
        </p>
        <VProgressLinear
          v-if="loading && !rows.length"
          indeterminate
          class="mb-4"
        />
        <VAlert
          v-if="!loading && !rows.length"
          type="info"
          variant="tonal"
        >
          لا توجد زيارات مسجّلة بعد.
        </VAlert>
        <VRow>
          <VCol
            v-for="row in rows"
            :key="pick(row, 'id', 'Id')"
            cols="12"
            md="6"
          >
            <VCard
              variant="outlined"
              class="h-100"
            >
              <VCardItem>
                <VCardTitle>
                  {{ pick(row, 'cityName', 'CityName') || pick(row, 'cityValue', 'CityValue') || '—' }}
                </VCardTitle>
                <VCardSubtitle>
                  {{ pick(row, 'openedByDisplayName', 'OpenedByDisplayName') || '—' }}
                  ·
                  <VChip
                    size="small"
                    :color="pick(row, 'status', 'Status') === 'Open' ? 'warning' : 'success'"
                    class="ms-1"
                  >
                    {{ pick(row, 'status', 'Status') === 'Open' ? 'مفتوحة' : 'مغلقة' }}
                  </VChip>
                </VCardSubtitle>
              </VCardItem>
              <VCardText>
                <div class="mb-2">
                  الوصول: {{ fmt(pick(row, 'arrivedAtUtc', 'ArrivedAtUtc')) }}
                </div>
                <div class="mb-2">
                  المغادرة: {{ fmt(pick(row, 'departedAtUtc', 'DepartedAtUtc')) }}
                </div>
                <div class="mb-2">
                  الأعضاء ({{ pick(row, 'memberCount', 'MemberCount') || (pick(row, 'members', 'Members') || []).length }}):
                  {{
                    (pick(row, 'members', 'Members') || [])
                      .map(m => pick(m, 'displayName', 'DisplayName'))
                      .filter(Boolean)
                      .join('، ') || '—'
                  }}
                </div>
                <div class="mb-2">
                  <strong>تقييمات المناديب</strong>
                  <ul class="mt-1 mb-0">
                    <li
                      v-for="r in (pick(row, 'delegateRatings', 'DelegateRatings') || [])"
                      :key="`${pick(r, 'delegateId', 'DelegateId')}-${pick(r, 'score', 'Score')}`"
                    >
                      {{ pick(r, 'delegateName', 'DelegateName') }}: {{ pick(r, 'score', 'Score') }}/10
                    </li>
                  </ul>
                  <span
                    v-if="!(pick(row, 'delegateRatings', 'DelegateRatings') || []).length"
                    class="text-medium-emphasis"
                  >لا تقييمات بعد</span>
                </div>
                <div
                  v-if="pick(row, 'notes', 'Notes')"
                  class="mb-2"
                >
                  <strong>ملاحظات المغادرة</strong>
                  <div class="text-body-2 mt-1 white-space-pre-wrap">
                    {{ pick(row, 'notes', 'Notes') }}
                  </div>
                </div>
                <div v-if="(pick(row, 'attachments', 'Attachments') || []).length">
                  مرفقات: {{ (pick(row, 'attachments', 'Attachments') || []).length }}
                </div>
              </VCardText>
            </VCard>
          </VCol>
        </VRow>
      </VCardText>
    </VCard>
  </div>
</template>
