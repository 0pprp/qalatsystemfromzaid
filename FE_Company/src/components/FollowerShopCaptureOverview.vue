<script setup>
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import {
  directorFollowerOpsShopCapture,
  directorFollowerOpsShopCaptureList,
  directorFollowerOpsShopCapturePhoto,
} from '@/composables/directorApi'

/**
 * عمل المتابعين — مواقع وصور المحلات.
 * لكل قائمة مسندة لمتابع: كم زبون أضاف المتابع موقع محله وصورته (نفس عدّ تطبيق المتابع)، ومن هم.
 */
const props = defineProps({
  cityValue: { type: String, default: '' },
})

const loading = ref(false)
const error = ref('')
const overview = ref(null)

const selected = ref(null)
const rowsLoading = ref(false)
const rowsError = ref('')
const rows = ref([])
const search = ref('')
const filter = ref('all')

const photoOpen = ref(false)
const photoUrl = ref('')
const photoTitle = ref('')
const photoLoading = ref(false)

const lists = computed(() => overview.value?.lists || [])

function pct(done, total) {
  return total ? Math.round(done * 100 / total) : 0
}

async function load() {
  selected.value = null
  rows.value = []
  overview.value = null
  error.value = ''
  if (!props.cityValue)
    return
  loading.value = true
  try {
    overview.value = await directorFollowerOpsShopCapture({ cityValue: props.cityValue })
  }
  catch (e) {
    error.value = e?.response?.data?.message || e?.message || 'تعذر التحميل'
  }
  finally {
    loading.value = false
  }
}

async function openList(l) {
  selected.value = l
  search.value = ''
  filter.value = 'all'
  rows.value = []
  rowsError.value = ''
  rowsLoading.value = true
  try {
    const data = await directorFollowerOpsShopCaptureList({ cityValue: props.cityValue, listId: l.listId })

    if (selected.value?.listId === l.listId)
      rows.value = Array.isArray(data?.items) ? data.items : []
  }
  catch (e) {
    rowsError.value = e?.response?.data?.message || e?.message || 'تعذر تحميل الزبائن'
  }
  finally {
    rowsLoading.value = false
  }
}

const hasLoc = r => r.followerLatitude != null && r.followerLongitude != null

const counts = computed(() => ({
  all: rows.value.length,
  noLocation: rows.value.filter(r => !hasLoc(r)).length,
  noPhoto: rows.value.filter(r => !r.hasPhoto).length,
  done: rows.value.filter(r => hasLoc(r) && r.hasPhoto).length,
}))

const shownRows = computed(() => {
  const q = search.value.trim().toLowerCase()

  return rows.value.filter(r => {
    if (filter.value === 'noLocation' && hasLoc(r))
      return false
    if (filter.value === 'noPhoto' && r.hasPhoto)
      return false
    if (filter.value === 'done' && !(hasLoc(r) && r.hasPhoto))
      return false
    if (!q)
      return true

    return [r.customerName, r.shopName, r.phoneNumber].some(v => String(v || '').toLowerCase().includes(q))
  })
})

function mapsUrl(lat, lng) {
  return `https://www.google.com/maps?q=${lat},${lng}`
}

function fmtDate(v) {
  return v ? String(v).slice(0, 10) : ''
}

async function openPhoto(r) {
  photoTitle.value = r.customerName
  photoOpen.value = true
  photoLoading.value = true
  if (photoUrl.value)
    URL.revokeObjectURL(photoUrl.value)
  photoUrl.value = ''
  try {
    const blob = await directorFollowerOpsShopCapturePhoto({ cityValue: props.cityValue, customerId: r.customerId })

    photoUrl.value = URL.createObjectURL(blob)
  }
  catch {
    photoUrl.value = ''
  }
  finally {
    photoLoading.value = false
  }
}

watch(() => props.cityValue, load, { immediate: true })

onBeforeUnmount(() => {
  if (photoUrl.value)
    URL.revokeObjectURL(photoUrl.value)
})

defineExpose({ reload: load })
</script>

<template>
  <section class="fsc">
    <div
      v-if="!cityValue"
      class="fsc-empty"
    >
      اختر المحافظة
    </div>
    <div
      v-else-if="loading"
      class="fsc-empty"
    >
      <VProgressCircular
        indeterminate
        color="primary"
      />
    </div>
    <div
      v-else-if="error"
      class="fsc-empty text-error"
    >
      {{ error }}
      <VBtn
        variant="text"
        size="small"
        @click="load"
      >
        إعادة
      </VBtn>
    </div>
    <template v-else-if="overview">
      <div class="fsc-total">
        <div class="fsc-total-num">
          <span>الزبائن</span><b>{{ overview.customerCount }}</b>
        </div>
        <div class="fsc-bar-row">
          <span class="fsc-bar-label">المواقع</span>
          <VProgressLinear
            :model-value="pct(overview.withLocation, overview.customerCount)"
            color="primary"
            height="10"
            rounded
          />
          <b>{{ overview.withLocation }}/{{ overview.customerCount }}</b>
        </div>
        <div class="fsc-bar-row">
          <span class="fsc-bar-label">الصور</span>
          <VProgressLinear
            :model-value="pct(overview.withPhoto, overview.customerCount)"
            color="warning"
            height="10"
            rounded
          />
          <b>{{ overview.withPhoto }}/{{ overview.customerCount }}</b>
        </div>
      </div>

      <div
        v-if="!lists.length"
        class="fsc-empty muted"
      >
        لا توجد قوائم مسندة لمتابعين في هذه المحافظة
      </div>
      <div
        v-else
        class="fsc-grid"
      >
        <button
          v-for="l in lists"
          :key="l.listId"
          type="button"
          class="fsc-card"
          :class="{ 'fsc-card-active': selected?.listId === l.listId }"
          @click="openList(l)"
        >
          <div class="fsc-card-name">
            {{ l.listName }}
          </div>
          <div
            v-if="l.followers?.length"
            class="fsc-card-sub"
          >
            {{ l.followers.join('، ') }}
          </div>
          <div class="fsc-bar-row">
            <span class="fsc-bar-label">المواقع</span>
            <VProgressLinear
              :model-value="pct(l.withLocation, l.customerCount)"
              color="primary"
              height="8"
              rounded
            />
            <b>{{ l.withLocation }}/{{ l.customerCount }}</b>
          </div>
          <div class="fsc-bar-row">
            <span class="fsc-bar-label">الصور</span>
            <VProgressLinear
              :model-value="pct(l.withPhoto, l.customerCount)"
              color="warning"
              height="8"
              rounded
            />
            <b>{{ l.withPhoto }}/{{ l.customerCount }}</b>
          </div>
        </button>
      </div>

      <div
        v-if="selected"
        class="fsc-list"
      >
        <div class="fsc-list-head">
          <h3>{{ selected.listName }}</h3>
          <VBtn
            variant="text"
            size="small"
            @click="selected = null"
          >
            إغلاق
          </VBtn>
        </div>
        <div class="fsc-list-tools">
          <VTextField
            v-model="search"
            density="compact"
            hide-details
            clearable
            prepend-inner-icon="tabler-search"
            placeholder="بحث بالاسم أو المحل أو الهاتف"
            class="fsc-search"
          />
          <VChipGroup
            v-model="filter"
            mandatory
            selected-class="text-primary"
          >
            <VChip
              value="all"
              size="small"
            >
              الكل ({{ counts.all }})
            </VChip>
            <VChip
              value="noLocation"
              size="small"
            >
              بدون موقع ({{ counts.noLocation }})
            </VChip>
            <VChip
              value="noPhoto"
              size="small"
            >
              بدون صورة ({{ counts.noPhoto }})
            </VChip>
            <VChip
              value="done"
              size="small"
            >
              مكتمل ({{ counts.done }})
            </VChip>
          </VChipGroup>
        </div>

        <div
          v-if="rowsLoading"
          class="fsc-empty"
        >
          <VProgressCircular
            indeterminate
            color="primary"
          />
        </div>
        <div
          v-else-if="rowsError"
          class="fsc-empty text-error"
        >
          {{ rowsError }}
        </div>
        <VTable
          v-else
          density="compact"
          class="fsc-table"
        >
          <thead>
            <tr>
              <th>الزبون</th>
              <th>المحل</th>
              <th>الموقع</th>
              <th>الصورة</th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="r in shownRows"
              :key="r.customerId"
            >
              <td>
                <div class="fsc-name">
                  {{ r.customerName }}
                </div>
                <div
                  v-if="r.phoneNumber"
                  class="muted fsc-small"
                  dir="ltr"
                >
                  {{ r.phoneNumber }}
                </div>
              </td>
              <td>{{ r.shopName || '—' }}</td>
              <td>
                <template v-if="hasLoc(r)">
                  <a
                    :href="mapsUrl(r.followerLatitude, r.followerLongitude)"
                    target="_blank"
                    rel="noopener noreferrer"
                    class="fsc-ok"
                  >
                    <VIcon
                      icon="tabler-circle-check"
                      size="18"
                    /> خرائط
                  </a>
                  <div class="muted fsc-small">
                    {{ r.locationBy }} {{ fmtDate(r.followerLocationAtUtc) }}
                  </div>
                </template>
                <span
                  v-else
                  class="muted"
                >—</span>
              </td>
              <td>
                <template v-if="r.hasPhoto">
                  <VBtn
                    size="small"
                    variant="tonal"
                    color="success"
                    prepend-icon="tabler-photo"
                    @click="openPhoto(r)"
                  >
                    عرض
                  </VBtn>
                  <div class="muted fsc-small">
                    {{ r.photoBy }} {{ fmtDate(r.photoAtUtc) }}
                  </div>
                </template>
                <span
                  v-else
                  class="muted"
                >—</span>
              </td>
            </tr>
            <tr v-if="!shownRows.length">
              <td
                colspan="4"
                class="fsc-empty muted"
              >
                لا يوجد زبائن
              </td>
            </tr>
          </tbody>
        </VTable>
      </div>
    </template>

    <VDialog
      v-model="photoOpen"
      max-width="720"
    >
      <VCard>
        <VCardTitle class="fsc-dialog-title">
          <span>صورة المحل — {{ photoTitle }}</span>
          <VBtn
            icon="tabler-x"
            variant="text"
            size="small"
            @click="photoOpen = false"
          />
        </VCardTitle>
        <VCardText class="fsc-photo-body">
          <VProgressCircular
            v-if="photoLoading"
            indeterminate
            color="primary"
          />
          <img
            v-else-if="photoUrl"
            :src="photoUrl"
            alt="صورة المحل"
            class="fsc-photo"
          >
          <span
            v-else
            class="muted"
          >تعذر تحميل الصورة</span>
        </VCardText>
      </VCard>
    </VDialog>
  </section>
</template>

<style scoped>
.fsc { display: flex; flex-direction: column; gap: 14px; margin-top: 12px; }
.fsc-empty { padding: 24px; text-align: center; }
.fsc-total {
  display: flex;
  flex-direction: column;
  gap: 6px;
  padding: 14px 16px;
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-radius: 14px;
  background: rgb(var(--v-theme-surface));
  max-width: 560px;
}
.fsc-total-num { display: flex; gap: 8px; align-items: baseline; }
.fsc-total-num b { font-size: 1.3rem; }
.fsc-bar-row { display: grid; grid-template-columns: 56px 1fr auto; align-items: center; gap: 10px; font-size: 0.85rem; }
.fsc-bar-label { opacity: 0.8; }
.fsc-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(260px, 1fr)); gap: 10px; }
.fsc-card {
  display: flex;
  flex-direction: column;
  gap: 4px;
  padding: 12px 14px;
  text-align: start;
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-radius: 14px;
  background: rgb(var(--v-theme-surface));
  cursor: pointer;
}
.fsc-card:hover { border-color: rgb(var(--v-theme-primary)); }
.fsc-card-active { border-color: rgb(var(--v-theme-primary)); box-shadow: 0 0 0 1px rgb(var(--v-theme-primary)); }
.fsc-card-name { font-weight: 700; }
.fsc-card-sub { font-size: 0.8rem; opacity: 0.7; }
.fsc-list {
  padding: 12px 14px;
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-radius: 14px;
  background: rgb(var(--v-theme-surface));
}
.fsc-list-head { display: flex; align-items: center; justify-content: space-between; }
.fsc-list-head h3 { margin: 0; font-size: 1.05rem; }
.fsc-list-tools { display: flex; flex-wrap: wrap; align-items: center; gap: 8px 14px; margin: 8px 0; }
.fsc-search { max-width: 320px; min-width: 200px; }
.fsc-name { font-weight: 600; }
.fsc-small { font-size: 0.75rem; }
.fsc-ok { display: inline-flex; align-items: center; gap: 4px; color: rgb(var(--v-theme-success)); text-decoration: none; font-weight: 600; }
.fsc-dialog-title { display: flex; align-items: center; justify-content: space-between; }
.fsc-photo-body { display: flex; justify-content: center; min-height: 120px; }
.fsc-photo { max-width: 100%; max-height: 70vh; border-radius: 8px; }
@media (max-width: 600px) {
  .fsc-table th:nth-child(2), .fsc-table td:nth-child(2) { display: none; }
}
</style>
