<script setup>
import { computed, ref } from 'vue'
import axios from 'axios'
import * as XLSX from 'xlsx'
import { useToast } from '@/composables/useToast'
import {
  formatHiddenFinancial,
  shouldHidePaidRemaining,
  visibleAmountRemaining,
} from '@/composables/legalFinancialVisibility'

const toast = useToast()
const apiUrl = localStorage.getItem('LinkCity') || ''
const fileInput = ref(null)
const busy = ref(false)
const result = ref(null)
const openedQuery = ref(null)

const queries = computed(() => result.value?.queries || [])
const exportRows = computed(() => result.value?.exportRows || [])
const nameHitCount = computed(() => Number(result.value?.nameHitCount || 0))
const phoneHitCount = computed(() => Number(result.value?.phoneHitCount || 0))
const uniqueCustomerCount = computed(() => Number(result.value?.uniqueCustomerCount || 0))

function getAuthHeaders() {
  const token = localStorage.getItem('Token') || localStorage.getItem('token') || ''

  return token ? { Authorization: `Bearer ${token}` } : {}
}

function pick(obj, ...keys) {
  if (!obj)
    return undefined
  for (const key of keys) {
    if (obj[key] != null && obj[key] !== '')
      return obj[key]
  }

  return undefined
}

function headerKey(header) {
  const t = String(header || '').trim().toLowerCase().replace(/\s+/g, ' ')
  if (['العميل', 'اسم الزبون', 'اسم العميل', 'customername', 'fullname', 'name', 'الاسم'].includes(t))
    return 'name'
  if (['الهاتف', 'هاتف', 'phone', 'phonenumber', 'mobile', 'الجوال'].includes(t))
    return 'phone'

  return ''
}

function parseQueries(buffer) {
  const workbook = XLSX.read(buffer, { type: 'array' })
  const sheet = workbook.Sheets[workbook.SheetNames[0]]
  if (!sheet)
    throw new Error('الملف لا يحتوي على ورقة')

  const rows = XLSX.utils.sheet_to_json(sheet, { header: 1, defval: '', raw: false })
  if (!rows.length)
    throw new Error('الملف فارغ')

  const header = rows[0] || []
  let nameIndex = -1
  let phoneIndex = -1
  header.forEach((cell, index) => {
    const key = headerKey(cell)
    if (key === 'name' && nameIndex < 0)
      nameIndex = index
    if (key === 'phone' && phoneIndex < 0)
      phoneIndex = index
  })

  if (nameIndex < 0 && phoneIndex < 0)
    throw new Error('يلزم عمود العميل و/أو الهاتف في الصف الأول (نفس نموذج تصدير العملاء)')

  const queries = []
  for (let i = 1; i < rows.length; i++) {
    const row = rows[i] || []
    const name = nameIndex >= 0 ? String(row[nameIndex] ?? '').trim() : ''
    const phone = phoneIndex >= 0 ? String(row[phoneIndex] ?? '').trim() : ''
    if (!name && !phone)
      continue
    queries.push({ name: name || null, phone: phone || null })
  }

  if (!queries.length)
    throw new Error('لا توجد صفوف صالحة في الملف')
  if (queries.length > 1500)
    throw new Error('عدد الصفوف أكبر من 1500. قسّم الملف ثم أعد المحاولة.')

  return queries
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
      const queries = parseQueries(e.target.result)
      busy.value = true
      openedQuery.value = null
      const { data } = await axios.post(
        `${apiUrl}Customers/LegalExcelSearch`,
        { queries },
        { headers: getAuthHeaders(), timeout: 180000 },
      )
      result.value = data
      toast.success(
        `تم البحث عن ${queries.length} صف — تطابق اسم: ${data.nameHitCount || 0} / هاتف: ${data.phoneHitCount || 0} / حسابات فريدة: ${data.uniqueCustomerCount || 0}`,
      )
    }
    catch (err) {
      result.value = null
      const msg = err?.response?.data?.message || err?.message || 'تعذر بحث القانونية'
      toast.error(msg)
    }
    finally {
      busy.value = false
    }
  }
  reader.readAsArrayBuffer(file)
}

function customerField(c, ...keys) {
  return pick(c, ...keys)
}

function mapCustomerToExcelRow(customer, meta = {}) {
  const c = customer || {}
  const rate = customerField(c, 'receiptRateDevice', 'ReceiptRateDevice')
  const dateSale = customerField(c, 'dateSaleDevice', 'DateSaleDevice')
  const lastPay = customerField(c, 'lastPaymentDate', 'LastPaymentDate')

  return {
    'الاسم المطلوب': meta.requestedName || 'لا يوجد',
    'الهاتف المطلوب': meta.requestedPhone || 'لا يوجد',
    'نوع التطابق': (meta.matchKinds || []).join('+') || 'لا يوجد',
    'العميل': customerField(c, 'customerName', 'CustomerName') || 'لا يوجد',
    'المباعة': customerField(c, 'itemsNames', 'ItemsNames') || 'لا يوجد',
    'التاريخ': dateSale ? new Date(dateSale).toLocaleDateString('en-CA') : 'لا يوجد',
    'سعر البيع': customerField(c, 'amountTotalSales', 'AmountTotalSales') ?? 'لا يوجد',
    'سعر الشراء': customerField(c, 'costTotalSales', 'CostTotalSales') ?? 'لا يوجد',
    'القسط': customerField(c, 'amountDaySales', 'AmountDaySales') ?? 'لا يوجد',
    'الواصل': shouldHidePaidRemaining(c)
      ? '—'
      : (customerField(c, 'receiptsTotal', 'ReceiptsTotal') ?? 'لا يوجد'),
    'الباقي': shouldHidePaidRemaining(c)
      ? '—'
      : (customerField(c, 'amountRemaining', 'AmountRemaining') ?? 'لا يوجد'),
    'نسبة التسديد': rate !== undefined && rate !== null ? `${rate}%` : '0%',
    'عدد التسديدات': customerField(c, 'countReceiptDevice', 'CountReceiptDevice') ?? 'لا يوجد',
    'عدد الأيام': customerField(c, 'numberOfDayDevice', 'NumberOfDayDevice') ?? 'لا يوجد',
    'تاريخ اخر تسديد': lastPay ? new Date(lastPay).toLocaleDateString('en-CA') : 'لا يوجد',
    'العنوان': customerField(c, 'address', 'Address') || 'لا يوجد',
    'الهاتف': customerField(c, 'phoneNumber', 'PhoneNumber') || 'لا يوجد',
    'اسم المحل': customerField(c, 'shopName', 'ShopName') || 'لا يوجد',
    'اقرب نقطة': customerField(c, 'nearestFunctionPoint', 'NearestFunctionPoint') || 'لا يوجد',
    'المندوب': customerField(c, 'delegateName', 'DelegateName') || 'لا يوجد',
    'اسم الجابي': customerField(c, 'receiptName', 'ReceiptName') || 'لا يوجد',
    'اسم البائع': customerField(c, 'saleName', 'SaleName') || 'لا يوجد',
    'الملاحظات': customerField(c, 'notes', 'Notes') || 'لا يوجد',
  }
}

function exportToExcel() {
  if (!exportRows.value.length) {
    toast.error('لا توجد نتائج للتصدير')

    return
  }

  const dataToExport = exportRows.value.map(row => mapCustomerToExcelRow(row.customer || row.Customer, {
    requestedName: row.requestedName || row.RequestedName,
    requestedPhone: row.requestedPhone || row.RequestedPhone,
    matchKinds: row.matchKinds || row.MatchKinds || [],
  }))

  const worksheet = XLSX.utils.json_to_sheet(dataToExport)
  const workbook = XLSX.utils.book_new()
  XLSX.utils.book_append_sheet(workbook, worksheet, 'Customers')
  XLSX.writeFile(workbook, 'بحث_القانونية.xlsx')
  toast.success(`تم تصدير ${dataToExport.length} صف (بدون تكرار نفس الحساب)`)
}

function queryTitle(q) {
  const name = q.requestedName || q.RequestedName || '—'
  const phone = q.requestedPhone || q.RequestedPhone || '—'

  return `${name} / ${phone}`
}

function nameMatches(q) {
  return q.nameMatches || q.NameMatches || []
}

function phoneMatches(q) {
  return q.phoneMatches || q.PhoneMatches || []
}
</script>

<template>
  <div>
    <VCard>
      <VCardTitle class="d-flex flex-wrap align-center justify-space-between gap-2">
        <span>بحث القانونية</span>
        <div class="d-flex flex-wrap gap-2">
          <VBtn
            color="primary"
            :loading="busy"
            :disabled="busy"
            @click="openPicker"
          >
            استيراد من إكسل
          </VBtn>
          <VBtn
            color="success"
            variant="tonal"
            :disabled="!exportRows.length || busy"
            @click="exportToExcel"
          >
            تصدير إلى إكسل
          </VBtn>
        </div>
      </VCardTitle>
      <VCardText>
        <p class="text-medium-emphasis mb-4">
          استورد ملف بنفس نموذج تصدير العملاء. يُستخدم عمودا <strong>العميل</strong> و<strong>الهاتف</strong> فقط.
          البحث على محافظة الدخول الحالية: تطابق الاسم الثلاثي الكامل (بدون إخوة) وتطابق الهاتف منفصلان؛ نفس الحساب لا يُكرَّر بالتصدير.
        </p>
        <input
          ref="fileInput"
          type="file"
          accept=".xlsx,.xls"
          class="d-none"
          @change="onFile"
        >
        <div
          v-if="result"
          class="d-flex flex-wrap gap-4 mb-4"
        >
          <VChip color="info">
            تطابق اسم: {{ nameHitCount }}
          </VChip>
          <VChip color="primary">
            تطابق هاتف: {{ phoneHitCount }}
          </VChip>
          <VChip color="success">
            حسابات فريدة للتصدير: {{ uniqueCustomerCount }}
          </VChip>
        </div>

        <VTable
          v-if="queries.length"
          density="compact"
        >
          <thead>
            <tr>
              <th>#</th>
              <th>المطلوب</th>
              <th>تطابق اسم</th>
              <th>تطابق هاتف</th>
              <th />
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="q in queries"
              :key="q.index"
            >
              <td>{{ (q.index ?? 0) + 1 }}</td>
              <td>{{ queryTitle(q) }}</td>
              <td>{{ nameMatches(q).length }}</td>
              <td>{{ phoneMatches(q).length }}</td>
              <td>
                <VBtn
                  size="small"
                  variant="text"
                  @click="openedQuery = openedQuery === q.index ? null : q.index"
                >
                  {{ openedQuery === q.index ? 'إخفاء' : 'تفاصيل' }}
                </VBtn>
              </td>
            </tr>
          </tbody>
        </VTable>

        <template v-if="openedQuery != null">
          <VDivider class="my-4" />
          <div
            v-for="q in queries.filter(x => x.index === openedQuery)"
            :key="`detail-${q.index}`"
          >
            <h4 class="mb-2">
              تطابق الاسم ({{ nameMatches(q).length }})
            </h4>
            <VTable
              v-if="nameMatches(q).length"
              density="compact"
              class="mb-4"
            >
              <thead>
                <tr>
                  <th>العميل</th>
                  <th>الهاتف</th>
                  <th>الباقي</th>
                  <th>نسبة التسديد</th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="c in nameMatches(q)"
                  :key="`n-${customerField(c, 'customerID', 'CustomerID')}`"
                >
                  <td>{{ customerField(c, 'customerName', 'CustomerName') }}</td>
                  <td>{{ customerField(c, 'phoneNumber', 'PhoneNumber') }}</td>
                  <td>{{ formatHiddenFinancial(visibleAmountRemaining(c)) }}</td>
                  <td>{{ customerField(c, 'receiptRateDevice', 'ReceiptRateDevice') }}%</td>
                </tr>
              </tbody>
            </VTable>
            <p
              v-else
              class="text-medium-emphasis"
            >
              لا تطابق بالاسم
            </p>

            <h4 class="mb-2">
              تطابق الهاتف ({{ phoneMatches(q).length }})
            </h4>
            <VTable
              v-if="phoneMatches(q).length"
              density="compact"
            >
              <thead>
                <tr>
                  <th>العميل</th>
                  <th>الهاتف</th>
                  <th>الباقي</th>
                  <th>نسبة التسديد</th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="c in phoneMatches(q)"
                  :key="`p-${customerField(c, 'customerID', 'CustomerID')}`"
                >
                  <td>{{ customerField(c, 'customerName', 'CustomerName') }}</td>
                  <td>{{ customerField(c, 'phoneNumber', 'PhoneNumber') }}</td>
                  <td>{{ formatHiddenFinancial(visibleAmountRemaining(c)) }}</td>
                  <td>{{ customerField(c, 'receiptRateDevice', 'ReceiptRateDevice') }}%</td>
                </tr>
              </tbody>
            </VTable>
            <p
              v-else
              class="text-medium-emphasis"
            >
              لا تطابق بالهاتف
            </p>
          </div>
        </template>
      </VCardText>
    </VCard>
  </div>
</template>
