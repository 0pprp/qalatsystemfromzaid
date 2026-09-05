<script setup>
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import SalesBranchFilter from '@/components/SalesBranchFilter.vue'
import { smGet, smPost } from '@/composables/salesManagerApi'
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
const notes = ref('')
const confirm = ref(false)

async function search() {
  if (query.value.trim().length < 2) return
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

async function send() {
  if (!cityValue.value) {
    toast.error('يجب اختيار المحافظة')

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

  await smPost('sales-requests', {
    cityValue: cityValue.value,
    existingCustomerId: sameBranch ? selectedCustomer.value?.customerId : null,
    customerSourceCityValue: sourceCity || null,
    customer,
    notes: notes.value,
  })
  toast.success('تم إنشاء الطلب غير مسند. يمكن إسناده من قائمة الطلبات.')
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
          title="المحافظة"
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
            الطلب يُنشأ غير مسند. إسناد الموظف يتم لاحقاً من تفاصيل الطلب.
          </p>
          <SalesBranchFilter
            v-model="cityValue"
            class="mt-2"
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
              <div>الإسناد: غير مسند — يتم لاحقاً من قائمة الطلبات</div>
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
        <VCardText>سيتم إنشاء الطلب غير مسند في المحافظة المختارة. الإسناد يتم لاحقاً بزر الإسناد.</VCardText>
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
