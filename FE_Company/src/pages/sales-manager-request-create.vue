<script setup>
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { locationStatusLabel, smGet, smPost } from '@/composables/salesManagerApi'
import { useToast } from '@/composables/useToast'

const toast = useToast()
const router = useRouter()
const step = ref(1)
const query = ref('')
const customers = ref([])
const selectedCustomer = ref(null)
const isNewCustomer = ref(false)
const newCustomer = ref({ fullName: '', phone: '', province: '', address: '' })
const employees = ref([])
const employeeId = ref(null)
const notes = ref('')
const confirm = ref(false)

const selectedEmployee = computed(() => employees.value.find(e => e.employeeId === employeeId.value))

onMounted(async () => {
  employees.value = await smGet('employees')
})

async function search() {
  if (query.value.trim().length < 2) return
  customers.value = await smGet(`customers/search?q=${encodeURIComponent(query.value.trim())}`)
}

async function send() {
  const customer = isNewCustomer.value
    ? newCustomer.value
    : {
      fullName: selectedCustomer.value.fullName || selectedCustomer.value.customerName,
      phone: selectedCustomer.value.phone,
      province: selectedCustomer.value.province,
      address: selectedCustomer.value.address,
    }

  await smPost('sales-requests', {
    targetEmployeeId: employeeId.value,
    existingCustomerId: isNewCustomer.value ? null : selectedCustomer.value?.customerId,
    customer,
    notes: notes.value,
  })
  toast.success('تم إرسال طلب المبيع بنجاح.')
  confirm.value = false
  router.push({ name: 'sales-manager-requests' })
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
          title="الفرع والموظف"
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
            @keyup.enter="search"
          />
          <VBtn
            class="mb-4"
            @click="search"
          >
            بحث
          </VBtn>
          <VList>
            <VListItem
              v-for="c in customers"
              :key="c.customerId"
              :title="c.fullName"
              :subtitle="`${c.phone || ''} — ${c.province || ''}`"
              @click="selectedCustomer = c; isNewCustomer = false"
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
          <VSelect
            v-model="employeeId"
            :items="employees"
            item-title="employeeName"
            item-value="employeeId"
            label="موظف المبيعات"
            class="mt-4"
          />
          <div
            v-if="selectedEmployee"
            class="mt-2"
          >
            {{ selectedEmployee.cityName }} —
            {{ selectedEmployee.shiftStatus === 'Active' ? 'الدوام فعال' : 'بدون دوام' }} —
            {{ locationStatusLabel[selectedEmployee.locationStatus] }}
          </div>
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
              <div>الزبون: {{ isNewCustomer ? newCustomer.fullName : selectedCustomer?.fullName }}</div>
              <div>الموظف: {{ selectedEmployee?.employeeName }}</div>
              <div>الملاحظة: {{ notes }}</div>
            </VCardText>
          </VCard>
          <VBtn
            color="primary"
            class="mt-4"
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
        <VCardText>هل تريد إرسال طلب المبيع إلى {{ selectedEmployee?.employeeName }}؟</VCardText>
        <VCardActions>
          <VBtn
            variant="text"
            @click="confirm = false"
          >
            رجوع
          </VBtn>
          <VBtn
            color="primary"
            @click="send"
          >
            إرسال
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>
  </div>
</template>
