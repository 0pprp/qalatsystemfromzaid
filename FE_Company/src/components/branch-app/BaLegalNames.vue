<script setup>
import { computed, ref, watch } from 'vue'
import { asList, branchApp, errorText, money, pick } from '@/composables/branchAppApi'
import { useToast } from '@/composables/useToast'

const props = defineProps({ city: { type: String, required: true } })
const toast = useToast()

const loading = ref(false)
const rows = ref([])
const search = ref('')

const dialog = ref(false)
const target = ref(null)
const decision = ref('')
const note = ref('')
const saving = ref(false)

const options = [
  { value: 'متواصل', title: 'متواصل', hint: 'إخفاء من القائمة لمدة 7 أيام' },
  { value: 'قانونية', title: 'قانونية', hint: 'يُرسل طلب تحويل بانتظار الموافقة' },
  { value: 'وهمي', title: 'وهمي', hint: 'تأشير الزبون كبيع وهمي' },
  { value: 'تنازل', title: 'تنازل', hint: 'طلب تنازل إلى المدير المفوض (الملاحظة إجبارية)' },
]

const headers = [
  { title: 'الزبون', key: 'name' },
  { title: 'الهاتف', key: 'phone' },
  { title: 'القائمة', key: 'list' },
  { title: 'نسبة الأسبوع', key: 'pct', align: 'end' },
  { title: 'البيع', key: 'total', align: 'end' },
  { title: 'هذا الأسبوع', key: 'week', align: 'end' },
  { title: 'الباقي', key: 'remain', align: 'end' },
  { title: '', key: 'actions', sortable: false, align: 'end' },
]

const items = computed(() => rows.value.map(r => ({
  raw: r,
  id: Number(pick(r, 'customerID', 'customerId', 'CustomerID', 'CustomerId') || 0),
  name: pick(r, 'customerName', 'CustomerName') || '—',
  phone: pick(r, 'phoneNumber', 'PhoneNumber') || '',
  list: pick(r, 'delegateName', 'DelegateName') || '',
  pct: Number(pick(r, 'paidPercent', 'PaidPercent') || 0),
  total: pick(r, 'amountTotalSales', 'AmountTotalSales'),
  week: pick(r, 'weekPaid', 'WeekPaid'),
  remain: pick(r, 'amountRemaining', 'AmountRemaining'),
})))

const noteRequired = computed(() => decision.value === 'تنازل')
const canSave = computed(() => !!decision.value && (!noteRequired.value || note.value.trim().length > 0))

async function load() {
  loading.value = true
  try {
    rows.value = asList(await branchApp.weakWeekPayers(props.city))
  }
  catch (e) {
    rows.value = []
    toast.error(errorText(e, 'تعذر تحميل أسماء قانونية'))
  }
  finally {
    loading.value = false
  }
}

function open(item) {
  target.value = item
  decision.value = ''
  note.value = ''
  dialog.value = true
}

async function save() {
  if (!target.value || !canSave.value)
    return
  saving.value = true
  try {
    await branchApp.decide(props.city, {
      customerId: target.value.id,
      decisionType: decision.value,
      note: note.value.trim() || null,
      customerName: target.value.name,
      customerPhone: target.value.phone || null,
    })
    toast.success(decision.value === 'تنازل' ? 'تم إرسال طلب التنازل' : `تم تسجيل القرار: ${decision.value}`)
    if (decision.value !== 'تنازل')
      rows.value = rows.value.filter(r => r !== target.value.raw)
    dialog.value = false
  }
  catch (e) {
    toast.error(errorText(e, 'تعذر تسجيل القرار'))
  }
  finally {
    saving.value = false
  }
}

watch(() => props.city, load, { immediate: true })
</script>

<template>
  <div>
    <div class="d-flex flex-wrap align-center ga-2 mb-3">
      <span class="text-body-2 text-medium-emphasis">
        زبائن تسديدهم الأسبوعي ضعيف وآخر تسديد منذ 40 يوماً أو أكثر — اختر قراراً لكل زبون
      </span>
      <VSpacer />
      <VTextField
        v-model="search"
        placeholder="بحث"
        density="compact"
        hide-details
        prepend-inner-icon="tabler-search"
        style="max-inline-size: 220px"
      />
      <VBtn
        variant="tonal"
        icon="tabler-refresh"
        size="small"
        :loading="loading"
        @click="load"
      />
    </div>

    <VDataTable
      :headers="headers"
      :items="items"
      :search="search"
      :loading="loading"
      :items-per-page="50"
      density="compact"
      items-per-page-text="عدد الصفوف"
      page-text="{0}-{1} من {2}"
      no-data-text="لا يوجد زبائن حالياً"
    >
      <template #item.pct="{ item }">
        {{ item.pct.toFixed(2) }}%
      </template>
      <template #item.total="{ item }">
        {{ money(item.total) }}
      </template>
      <template #item.week="{ item }">
        {{ money(item.week) }}
      </template>
      <template #item.remain="{ item }">
        {{ money(item.remain) }}
      </template>
      <template #item.actions="{ item }">
        <VBtn
          size="small"
          variant="tonal"
          color="primary"
          @click="open(item)"
        >
          قرار
        </VBtn>
      </template>
    </VDataTable>

    <VDialog
      v-model="dialog"
      max-width="480"
    >
      <VCard :title="target?.name">
        <VCardText>
          <VRadioGroup
            v-model="decision"
            hide-details
          >
            <VRadio
              v-for="o in options"
              :key="o.value"
              :value="o.value"
              class="mb-2"
            >
              <template #label>
                <div class="py-1">
                  <div class="font-weight-medium">{{ o.title }}</div>
                  <div class="text-caption text-medium-emphasis">
                    {{ o.hint }}
                  </div>
                </div>
              </template>
            </VRadio>
          </VRadioGroup>
          <VTextarea
            v-model="note"
            class="mt-3"
            :label="noteRequired ? 'الملاحظة (إجبارية)' : 'ملاحظة (اختياري)'"
            rows="3"
            auto-grow
          />
        </VCardText>
        <VCardActions>
          <VSpacer />
          <VBtn
            variant="text"
            @click="dialog = false"
          >
            إلغاء
          </VBtn>
          <VBtn
            color="primary"
            :disabled="!canSave"
            :loading="saving"
            @click="save"
          >
            حفظ القرار
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>
  </div>
</template>
