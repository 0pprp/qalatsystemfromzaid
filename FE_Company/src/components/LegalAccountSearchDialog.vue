<script setup>
import { computed, ref, watch } from 'vue'
import { accountKey, legalError, legalDay, legalSearch, legalMoney } from '@/composables/legalDeskApi'

/**
 * Finds a debtor's other sales in every branch by name or phone (finished sales included)
 * and hands back the rows the user ticks.
 */
const props = defineProps({
  modelValue: { type: Boolean, default: false },
  initialQuery: { type: String, default: '' },
  /** accountKey()s already chosen elsewhere: shown as picked and not offered again. */
  taken: { type: Array, default: () => [] },
  confirmLabel: { type: String, default: 'إضافة المختار' },
})

const emit = defineEmits(['update:modelValue', 'pick'])

const query = ref('')
const loading = ref(false)
const searched = ref(false)
const rows = ref([])
const failed = ref([])
const error = ref('')
const picked = ref([])

const takenSet = computed(() => new Set(props.taken))

watch(() => props.modelValue, open => {
  if (!open)
    return
  query.value = props.initialQuery || ''
  rows.value = []
  failed.value = []
  picked.value = []
  error.value = ''
  searched.value = false
  if (query.value.trim().length >= 3)
    search()
})

async function search() {
  if (query.value.trim().length < 3) {
    error.value = 'اكتب ثلاثة أحرف على الأقل'

    return
  }
  loading.value = true
  error.value = ''
  try {
    const res = await legalSearch(query.value.trim())

    rows.value = res.rows || []
    failed.value = res.failedBranches || []
    searched.value = true
  }
  catch (e) {
    error.value = legalError(e, 'تعذر البحث')
  }
  finally {
    loading.value = false
  }
}

function blocked(row) {
  return Boolean(row.personId) || takenSet.value.has(accountKey(row))
}

function toggle(row) {
  if (blocked(row))
    return
  const key = accountKey(row)
  const i = picked.value.findIndex(r => accountKey(r) === key)
  if (i >= 0)
    picked.value.splice(i, 1)
  else
    picked.value.push(row)
}

const isPicked = row => picked.value.some(r => accountKey(r) === accountKey(row))

function confirm() {
  emit('pick', picked.value.slice())
  emit('update:modelValue', false)
}
</script>

<template>
  <VDialog
    :model-value="modelValue"
    max-width="1100"
    scrollable
    @update:model-value="emit('update:modelValue', $event)"
  >
    <VCard>
      <VCardTitle class="d-flex align-center gap-2">
        <VIcon icon="tabler-search" />
        البحث عن حسابات المدين في كل الفروع
      </VCardTitle>
      <VCardText>
        <div class="d-flex gap-2 mb-2">
          <VTextField
            v-model="query"
            label="الاسم أو رقم الهاتف"
            density="compact"
            hide-details
            autofocus
            clearable
            @keyup.enter="search"
          />
          <VBtn
            color="primary"
            :loading="loading"
            @click="search"
          >
            بحث
          </VBtn>
        </div>
        <div class="text-caption text-medium-emphasis mb-2">
          يظهر هنا كل حساب للزبون حتى المنتهي تسديده، لأن الملف القانوني يُبنى على الشخص لا على البيع.
        </div>
        <VAlert
          v-if="error"
          type="error"
          variant="tonal"
          density="compact"
          class="mb-2"
        >
          {{ error }}
        </VAlert>
        <VAlert
          v-if="failed.length"
          type="warning"
          variant="tonal"
          density="compact"
          class="mb-2"
        >
          لم تُجب هذه الفروع، قد توجد فيها حسابات أخرى: {{ failed.join('، ') }}
        </VAlert>

        <VTable
          v-if="rows.length"
          density="compact"
          fixed-header
          height="420"
        >
          <thead>
            <tr>
              <th />
              <th>الاسم</th>
              <th>الهاتف</th>
              <th>الفرع</th>
              <th>القائمة</th>
              <th>المواد</th>
              <th>تاريخ البيع</th>
              <th class="text-end">
                المبلغ
              </th>
              <th class="text-end">
                الباقي
              </th>
              <th>الحالة</th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="row in rows"
              :key="accountKey(row)"
              :class="{ 'text-disabled': blocked(row) }"
              style="cursor: pointer;"
              @click="toggle(row)"
            >
              <td>
                <VCheckboxBtn
                  :model-value="isPicked(row)"
                  :disabled="blocked(row)"
                  density="compact"
                  @click.stop="toggle(row)"
                />
              </td>
              <td>
                {{ row.customerName }}
                <VChip
                v-if="row.archiveCode"
                size="x-small"
                color="primary"
                variant="tonal"
                class="ms-1"
                style="direction: ltr; unicode-bidi: isolate; font-family: Consolas, monospace; font-weight: 700;"
              >
                {{ row.archiveCode }}
              </VChip>
              </td>
              <td dir="ltr">
                {{ row.phone || '—' }}
              </td>
              <td>{{ row.cityName }}</td>
              <td>{{ row.listName || '—' }}</td>
              <td class="text-truncate" style="max-inline-size: 180px;">
                {{ row.items || '—' }}
              </td>
              <td>{{ legalDay(row.saleDate) }}</td>
              <td class="text-end">
                {{ legalMoney(row.total) }}
              </td>
              <td class="text-end font-weight-medium">
                {{ legalMoney(row.remaining) }}
              </td>
              <td>
                <VChip
                  v-if="row.personId"
                  size="x-small"
                  color="info"
                  variant="tonal"
                >
                  ضمن: {{ row.personName }}
                </VChip>
                <VChip
                  v-else-if="takenSet.has(accountKey(row))"
                  size="x-small"
                  variant="tonal"
                >
                  مختار
                </VChip>
                <VChip
                  v-else-if="row.remaining <= 0"
                  size="x-small"
                  color="success"
                  variant="tonal"
                >
                  منتهي
                </VChip>
                <VChip
                  v-else-if="row.isLegal"
                  size="x-small"
                  color="warning"
                  variant="tonal"
                >
                  قانونية
                </VChip>
              </td>
            </tr>
          </tbody>
        </VTable>
        <div
          v-else-if="searched && !loading"
          class="text-center text-medium-emphasis py-6"
        >
          لا توجد حسابات مطابقة
        </div>
      </VCardText>
      <VCardActions>
        <VSpacer />
        <VBtn
          variant="text"
          @click="emit('update:modelValue', false)"
        >
          إغلاق
        </VBtn>
        <VBtn
          color="primary"
          variant="flat"
          :disabled="!picked.length"
          @click="confirm"
        >
          {{ confirmLabel }} ({{ picked.length }})
        </VBtn>
      </VCardActions>
    </VCard>
  </VDialog>
</template>
