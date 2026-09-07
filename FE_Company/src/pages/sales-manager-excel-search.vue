<script setup>
import { computed, ref } from 'vue'
import * as XLSX from 'xlsx'
import SalesBranchFilter from '@/components/SalesBranchFilter.vue'
import { smErrorMessage, smPost, withCityQuery } from '@/composables/salesManagerApi'
import { useToast } from '@/composables/useToast'

const toast = useToast()
const cityValue = ref('')
const fileInput = ref(null)
const busy = ref(false)
const result = ref(null)
const openedQuery = ref(null)

const queries = computed(() => result.value?.queries || result.value?.Queries || [])
const foundCount = computed(() => Number(result.value?.foundCount ?? result.value?.FoundCount ?? 0))
const missingCount = computed(() => Number(result.value?.missingCount ?? result.value?.MissingCount ?? 0))

function pick(obj, ...keys) {
  if (!obj)
    return undefined
  for (const key of keys) {
    if (obj[key] != null && obj[key] !== '')
      return obj[key]
  }
  return undefined
}

function money(value) {
  const n = Number(value)
  if (!Number.isFinite(n))
    return '0'
  return `${Math.round(n).toLocaleString('en-US')} د.ع`
}

function ymd(value) {
  if (!value)
    return ''
  const d = new Date(value)
  if (Number.isNaN(d.getTime()))
    return String(value)
  const yyyy = d.getFullYear()
  const mm = String(d.getMonth() + 1).padStart(2, '0')
  const dd = String(d.getDate()).padStart(2, '0')
  return `${yyyy}/${mm}/${dd}`
}

function headerKey(header) {
  const t = String(header || '').trim().toLowerCase().replace(/\s+/g, ' ')
  if (['اسم الزبون', 'اسم العميل', 'customername', 'fullname', 'name', 'الاسم'].includes(t))
    return 'name'
  return ''
}

function parseNames(buffer) {
  const workbook = XLSX.read(buffer, { type: 'array' })
  const sheet = workbook.Sheets[workbook.SheetNames[0]]
  if (!sheet)
    throw new Error('الملف لا يحتوي على ورقة')
  const rows = XLSX.utils.sheet_to_json(sheet, { header: 1, defval: '', raw: false })
  if (!rows.length)
    throw new Error('الملف فارغ')
  let nameIndex = 0
  const header = rows[0] || []
  header.forEach((cell, index) => {
    if (headerKey(cell) === 'name')
      nameIndex = index
  })
  if (headerKey(header[nameIndex]) !== 'name' && header.length > 1)
    throw new Error('عمود اسم الزبون مطلوب في الصف الأول')
  const names = []
  const start = headerKey(header[nameIndex]) === 'name' ? 1 : 0
  for (let i = start; i < rows.length; i++) {
    const raw = rows[i]?.[nameIndex]
    if (raw == null || String(raw).trim() === '')
      continue
    names.push(String(raw))
  }
  if (!names.length)
    throw new Error('لا توجد أسماء في الملف')
  if (names.length > 1500)
    throw new Error('عدد الأسماء أكبر من 1500. قسّم الملف ثم أعد المحاولة.')
  return names
}

function downloadTemplate() {
  const sheet = XLSX.utils.aoa_to_sheet([
    ['اسم الزبون'],
    ['حسين محمد'],
    ['علي جاسم'],
    ['حسين محمد 2'],
  ])
  const book = XLSX.utils.book_new()
  XLSX.utils.book_append_sheet(book, sheet, 'اسماء الزبائن')
  XLSX.writeFile(book, 'نموذج_بحث_الزبائن.xlsx')
}

function openPicker() {
  fileInput.value?.click()
}

function onFile(event) {
  const file = event.target.files?.[0]
  event.target.value = ''
  if (!file)
    return
  const reader = new FileReader()
  reader.onload = async e => {
    try {
      const names = parseNames(e.target.result)
      busy.value = true
      openedQuery.value = null
      const body = { names }
      if (cityValue.value)
        body.cityValue = cityValue.value
      result.value = await smPost(withCityQuery('customers/excel-search', cityValue.value), body)
      toast.success(`تم البحث عن ${names.length} اسماً. موجود: ${foundCount.value} / غير موجود: ${missingCount.value}`)
    }
    catch (err) {
      result.value = null
      toast.error(smErrorMessage(err, err?.message || 'تعذر البحث من Excel'))
    }
    finally {
      busy.value = false
    }
  }
  reader.readAsArrayBuffer(file)
}

function queryMatches(query) {
  return pick(query, 'matches', 'Matches') || []
}

function flattenReportRows() {
  const rows = []
  for (const query of queries.value) {
    const requested = pick(query, 'requestedName', 'RequestedName') || ''
    const matches = queryMatches(query)
    if (!matches.length) {
      rows.push({
        'الاسم المطلوب': requested,
        'الاسم المطابق': '',
        'الفرع': '',
        'الهاتف': '',
        'المحافظة': '',
        'العنوان': '',
        'القائمة/المندوب': '',
        'تاريخ البيع': '',
        'قيمة البيع': '',
        'المستلم': '',
        'المتبقي': '',
      })
      continue
    }
    for (const match of matches) {
      const sales = pick(match, 'sales', 'Sales') || []
      const base = {
        'الاسم المطلوب': requested,
        'الاسم المطابق': pick(match, 'fullName', 'FullName') || '',
        'الفرع': humanBranch(match),
        'الهاتف': pick(match, 'phone', 'Phone') || '',
        'المحافظة': pick(match, 'province', 'Province') || humanBranch(match),
        'العنوان': pick(match, 'address', 'Address') || '',
        'القائمة/المندوب': pick(match, 'delegateName', 'DelegateName') || '',
      }
      if (!sales.length) {
        rows.push({
          ...base,
          'تاريخ البيع': '',
          'قيمة البيع': '',
          'المستلم': pick(match, 'receiptsTotal', 'ReceiptsTotal') ?? '',
          'المتبقي': pick(match, 'amountRemaining', 'AmountRemaining') ?? '',
        })
        continue
      }
      for (const sale of sales) {
        rows.push({
          ...base,
          'تاريخ البيع': ymd(pick(sale, 'saleDate', 'SaleDate')),
          'قيمة البيع': pick(sale, 'saleAmount', 'SaleAmount') ?? '',
          'المستلم': pick(sale, 'receiptsTotal', 'ReceiptsTotal') ?? pick(match, 'receiptsTotal', 'ReceiptsTotal') ?? '',
          'المتبقي': pick(sale, 'amountRemaining', 'AmountRemaining') ?? pick(match, 'amountRemaining', 'AmountRemaining') ?? '',
        })
      }
    }
  }
  return rows
}

function humanBranch(match) {
  const name = pick(match, 'cityName', 'CityName', 'province', 'Province') || ''
  if (!name || /^database/i.test(name) || !name.includes(' ') && /_/i.test(name) && /demo/i.test(name))
    return ''
  return name
}

function searchKey(query) {
  return pick(query, 'searchKey', 'SearchKey') || ''
}

function usedFamilySearch(query) {
  return !!pick(query, 'usedFamilySearch', 'UsedFamilySearch')
}

function matchTitle(match) {
  const name = pick(match, 'fullName', 'FullName') || ''
  const branch = humanBranch(match)
  return branch ? `${name} — ${branch}` : name
}

function downloadReport() {
  if (!queries.value.length) {
    toast.error('ارفع ملف Excel أولاً')
    return
  }
  const sheet = XLSX.utils.json_to_sheet(flattenReportRows())
  const book = XLSX.utils.book_new()
  XLSX.utils.book_append_sheet(book, sheet, 'نتائج البحث')
  XLSX.writeFile(book, 'تقرير_بحث_الزبائن.xlsx')
}

function statusLabel(query) {
  return pick(query, 'found', 'Found') ? 'موجود' : 'غير موجود'
}
</script>

<template>
  <div>
    <h4 class="mb-2">
      بحث الزبائن من Excel
    </h4>
    <p class="text-medium-emphasis mb-4">
      ارفع قائمة أسماء فقط. النظام يبحث عنها في الزبائن والمبيعات ويعرض كل النتائج المطابقة دون إنشاء طلبات بيع أو تعديل أي بيانات.
    </p>

    <VRow class="mb-4">
      <VCol
        md="4"
        cols="12"
      >
        <SalesBranchFilter
          v-model="cityValue"
          @change="result = null"
        />
        <div class="text-caption text-medium-emphasis mt-1">
          اترك المحافظة فارغة للبحث في كل الفروع المتاحة لمسؤول المبيعات المركزي.
        </div>
      </VCol>
      <VCol
        md="8"
        cols="12"
        class="d-flex flex-wrap gap-2 align-end"
      >
        <VBtn
          variant="outlined"
          @click="downloadTemplate"
        >
          تحميل النموذج
        </VBtn>
        <VBtn
          color="primary"
          :loading="busy"
          @click="openPicker"
        >
          رفع Excel والبحث
        </VBtn>
        <VBtn
          color="success"
          :disabled="!queries.length"
          @click="downloadReport"
        >
          تنزيل التقرير
        </VBtn>
      </VCol>
    </VRow>
    <input
      ref="fileInput"
      type="file"
      accept=".xlsx,.xls"
      class="d-none"
      @change="onFile"
    >

    <VAlert
      v-if="result"
      type="info"
      variant="tonal"
      class="mb-4"
    >
      موجود: {{ foundCount }} — غير موجود: {{ missingCount }}
    </VAlert>

    <VTable v-if="queries.length">
      <thead>
        <tr>
          <th>الاسم الموجود في Excel</th>
          <th>مفتاح البحث</th>
          <th>الحالة</th>
          <th>عدد النتائج</th>
        </tr>
      </thead>
      <tbody>
        <tr
          v-for="(query, index) in queries"
          :key="index"
        >
          <td>
            <div>{{ pick(query, 'requestedName', 'RequestedName') }}</div>
          </td>
          <td>
            <span v-if="usedFamilySearch(query)">{{ searchKey(query) }}</span>
            <span
              v-else
              class="text-medium-emphasis"
            >—</span>
          </td>
          <td>
            <VChip
              size="small"
              :color="pick(query, 'found', 'Found') ? 'success' : 'warning'"
            >
              {{ statusLabel(query) }}
            </VChip>
            <div
              v-if="pick(query, 'warning', 'Warning')"
              class="text-caption text-warning"
            >
              {{ pick(query, 'warning', 'Warning') }}
            </div>
          </td>
          <td>
            <VBtn
              v-if="pick(query, 'matchCount', 'MatchCount')"
              variant="text"
              color="primary"
              @click="openedQuery = query"
            >
              {{ pick(query, 'matchCount', 'MatchCount') }} نتائج
            </VBtn>
            <span v-else>0</span>
          </td>
        </tr>
      </tbody>
    </VTable>

    <VDialog
      :model-value="!!openedQuery"
      max-width="1100px"
      @update:model-value="val => { if (!val) openedQuery = null }"
    >
      <VCard v-if="openedQuery">
        <VCardTitle class="d-flex justify-space-between align-start">
          <div>
            <div>نتائج: {{ pick(openedQuery, 'requestedName', 'RequestedName') }}</div>
            <div
              v-if="usedFamilySearch(openedQuery)"
              class="text-body-2 text-medium-emphasis mt-1"
            >
              مفتاح البحث: {{ searchKey(openedQuery) }}
            </div>
          </div>
          <VBtn
            icon
            variant="text"
            @click="openedQuery = null"
          >
            <VIcon icon="tabler-x" />
          </VBtn>
        </VCardTitle>
        <VCardText>
          <div
            v-for="(match, matchIdx) in queryMatches(openedQuery)"
            :key="matchIdx"
            class="mb-6 pa-4 rounded border"
          >
            <div class="text-h6 font-weight-bold mb-3">
              {{ matchTitle(match) }}
            </div>
            <div class="customer-facts mb-4">
              <div>الهاتف: {{ pick(match, 'phone', 'Phone') || '—' }}</div>
              <div>المحافظة: {{ pick(match, 'province', 'Province') || humanBranch(match) || '—' }}</div>
              <div>العنوان: {{ pick(match, 'address', 'Address') || '—' }}</div>
              <div>القائمة/المندوب: {{ pick(match, 'delegateName', 'DelegateName') || '—' }}</div>
            </div>
            <VTable>
              <thead>
                <tr>
                  <th>تاريخ البيع</th>
                  <th>قيمة البيع</th>
                  <th>المستلم</th>
                  <th>المتبقي</th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="(sale, sIdx) in (pick(match, 'sales', 'Sales') || [])"
                  :key="sIdx"
                >
                  <td>{{ ymd(pick(sale, 'saleDate', 'SaleDate')) }}</td>
                  <td>{{ money(pick(sale, 'saleAmount', 'SaleAmount')) }}</td>
                  <td>{{ money(pick(sale, 'receiptsTotal', 'ReceiptsTotal')) }}</td>
                  <td>{{ money(pick(sale, 'amountRemaining', 'AmountRemaining')) }}</td>
                </tr>
                <tr v-if="!(pick(match, 'sales', 'Sales') || []).length">
                  <td
                    colspan="4"
                    class="text-medium-emphasis"
                  >
                    لا توجد مبيعات رسمية مرتبطة بهذا الزبون.
                  </td>
                </tr>
              </tbody>
            </VTable>
          </div>
        </VCardText>
      </VCard>
    </VDialog>
  </div>
</template>

<style scoped>
.customer-facts {
  display: flex;
  flex-wrap: wrap;
  gap: 0.65rem 1.75rem;
  font-size: 1.05rem;
  line-height: 1.75;
}
</style>
