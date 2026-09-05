<script setup>
import { computed, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import SalesBranchFilter from '@/components/SalesBranchFilter.vue'
import { isDemo } from '@/composables/useCities'
import { smGet, smGetEmployees, smPost } from '@/composables/salesManagerApi'
import { useToast } from '@/composables/useToast'

const toast = useToast()
const router = useRouter()
const step = ref(1)
const query = ref('')
const customers = ref([])
const selectedCustomer = ref(null)
const isNewCustomer = ref(false)
const newCustomer = ref({ fullName: '', phone: '', province: '', address: '' })
const cityValue = ref('')
const employees = ref([])
const employeeId = ref(null)
const notes = ref('')
const confirm = ref(false)
const busy = ref(false)

async function searchCustomers() {
  if (query.value.trim().length < 2)
    return
  customers.value = await smGet(`customers/search?q=${encodeURIComponent(query.value.trim())}`)
}

function customerTitle(c) {
  const name = c.customerName || c.fullName || ''
  const city = c.cityName || c.branchName || c.province || ''

  return city ? `${name} — ${city}` : name
}

function customerKey(c) {
  return c.branchKey || `${c.cityValue || ''}:${c.customerId}`
}

function selectCustomer(c) {
  selectedCustomer.value = c
  isNewCustomer.value = false
  if (c.cityValue)
    cityValue.value = String(c.cityValue)
}

const selectedEmployee = computed(() => employees.value.find(e => e.employeeId === employeeId.value))

async function loadEmployees() {
  employeeId.value = null
  if (!cityValue.value) {
    employees.value = []

    return
  }
  try {
    employees.value = await smGetEmployees(cityValue.value)
  }
  catch {
    employees.value = []
  }
}

watch(cityValue, loadEmployees)

async function send() {
  if (!cityValue.value) {
    toast.error('يجب اختيار المحافظة')

    return
  }
  if (!employeeId.value) {
    toast.error('يجب اختيار موظف المبيعات')

    return
  }

  const customer = isNewCustomer.value
    ? newCustomer.value
    : {
      fullName: selectedCustomer.value.fullName || selectedCustomer.value.customerName,
      phone: selectedCustomer.value.phone,
      province: selectedCustomer.value.province || selectedCustomer.value.cityName,
      address: selectedCustomer.value.address,
    }

  const sourceCity = selectedCustomer.value?.cityValue
  const sameBranch = !isNewCustomer.value
    && sourceCity
    && String(sourceCity) === String(cityValue.value)
  const employee = selectedEmployee.value

  busy.value = true
  try {
    const created = await smPost('sales-requests', {
      cityValue: cityValue.value,
      existingCustomerId: sameBranch ? selectedCustomer.value?.customerId : null,
      customerSourceCityValue: sourceCity || null,
      customer,
      notes: notes.value,
      targetEmployeeId: employeeId.value,
      targetEmployeeName: employee?.employeeName,
    })
    const requestId = created?.id || created?.Id
    const assignPath = isDemo()
      ? `sales-requests/${requestId}/assign`
      : `sales-requests/${encodeURIComponent(cityValue.value)}/${requestId}/assign`
    await smPost(assignPath, {
      employeeId: employeeId.value,
      employeeName: employee?.employeeName,
      cityValue: cityValue.value,
      cityName: employee?.cityName || employee?.branchName,
    })
    toast.success('تم إنشاء الطلب وإسناده للموظف')
    confirm.value = false
    router.push({ name: 'sales-manager-requests' })
  }
  catch (err) {
    toast.error(err?.response?.data?.message || 'تعذر إنشاء الطلب')
  }
  finally {
    busy.value = false
  }
}
</script>

<template>
  <div>
    <h4 class="mb-4">
      طلب مبيع جديد
    </h4>
    <VStepper v-model="step">
      <VStepperHeader>
        <VStepperItem
          :value="1"
          title="الزبون"
        />
        <VStepperItem
          :value="2"
          title="المحافظة والموظف"
        />
        <VStepperItem
          :value="3"
          title="الملاحظة"
        />
        <VStepperItem
          :value="4"
          title="مراجعة وإرسال"
        />
      </VStepperHeader>
      <VStepperWindow>
        <VStepperWindowItem :value="1">
          <VTextField
            v-model="query"
            label="بحث عن زبون"
            class="mt-4"
            @keyup.enter="searchCustomers"
          />
          <VBtn
            class="mb-4"
            @click="searchCustomers"
          >
            بحث
          </VBtn>
          <VList>
            <VListItem
              v-for="c in customers"
              :key="customerKey(c)"
              :title="customerTitle(c)"
              :subtitle="c.phone || ''"
              :active="selectedCustomer && customerKey(selectedCustomer) === customerKey(c)"
              @click="selectCustomer(c)"
            />
          </VList>
          <VBtn
            variant="text"
            @click="isNewCustomer = true"
          >
            زبون جديد
          </VBtn>
          <div
            v-if="isNewCustomer"
            class="mt-4"
          >
            <VTextField
              v-model="newCustomer.fullName"
              label="الاسم *"
            />
            <VTextField
              v-model="newCustomer.phone"
              label="الهاتف *"
            />
            <VTextField
              v-model="newCustomer.province"
              label="المحافظة *"
            />
            <VTextField
              v-model="newCustomer.address"
              label="العنوان"
            />
          </div>
        </VStepperWindowItem>
        <VStepperWindowItem :value="2">
          <p class="mt-4 text-medium-emphasis">
            اختر المحافظة وموظف المبيعات قبل الإرسال. الطلبات المستوردة من Excel تبقى غير مسندة.
          </p>
          <SalesBranchFilter
            v-model="cityValue"
            class="mt-2"
          />
          <VSelect
            v-model="employeeId"
            class="mt-3"
            :items="employees"
            item-title="employeeName"
            item-value="employeeId"
            label="موظف المبيعات *"
            :disabled="!cityValue"
          />
        </VStepperWindowItem>
        <VStepperWindowItem :value="3">
          <VTextarea
            v-model="notes"
            label="ملاحظة الطلب"
            class="mt-4"
          />
        </VStepperWindowItem>
        <VStepperWindowItem :value="4">
          <VCard class="mt-4">
            <VCardText>
              <div>الزبون: {{ isNewCustomer ? newCustomer.fullName : customerTitle(selectedCustomer || {}) }}</div>
              <div>المحافظة المستهدفة: {{ cityValue || 'غير محددة' }}</div>
              <div>الموظف: {{ selectedEmployee?.employeeName || 'غير محدد' }}</div>
              <div>الملاحظة: {{ notes }}</div>
            </VCardText>
          </VCard>
          <VBtn
            color="primary"
            class="mt-4"
            :disabled="!cityValue || !employeeId"
            @click="confirm = true"
          >
            إرسال طلب المبيع
          </VBtn>
        </VStepperWindowItem>
      </VStepperWindow>
    </VStepper>
    <div class="mt-4">
      <VBtn
        v-if="step > 1"
        variant="text"
        @click="step--"
      >
        رجوع
      </VBtn>
      <VBtn
        v-if="step < 4"
        color="primary"
        @click="step++"
      >
        التالي
      </VBtn>
    </div>
    <VDialog
      v-model="confirm"
      max-width="420"
    >
      <VCard>
        <VCardTitle>تأكيد الإرسال</VCardTitle>
        <VCardText>
          سيتم إنشاء الطلب وإسناده إلى {{ selectedEmployee?.employeeName }}.
        </VCardText>
        <VCardActions>
          <VBtn
            variant="text"
            @click="confirm = false"
          >
            رجوع
          </VBtn>
          <VBtn
            color="primary"
            :loading="busy"
            @click="send"
          >
            إرسال
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>
  </div>
</template>
