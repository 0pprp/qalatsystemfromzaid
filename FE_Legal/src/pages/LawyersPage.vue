<script setup>
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { api, errorMessage } from '../api'
import { money, toast } from '../shared'

const router = useRouter()
const loading = ref(false)
const lawyers = ref([])
const showInactive = ref(false)

const shown = computed(() => lawyers.value.filter(l => showInactive.value || l.isActive))
const totalWallets = computed(() => lawyers.value.reduce((sum, l) => sum + Number(l.balance || 0), 0))

const dialog = ref(false)
const form = ref({ name: '', branch: '', phone: '', notes: '' })
const saving = ref(false)

async function load() {
  loading.value = true
  try {
    lawyers.value = await api.lawyers()
  }
  catch (e) {
    toast.error(errorMessage(e, 'تعذر تحميل المحامين'))
  }
  finally {
    loading.value = false
  }
}

function openNew() {
  form.value = { name: '', branch: '', phone: '', notes: '' }
  dialog.value = true
}

async function save() {
  if (!form.value.name.trim()) {
    toast.error('اكتب اسم المحامي')

    return
  }
  saving.value = true
  try {
    const { id } = await api.saveLawyer({ ...form.value, isActive: true })

    dialog.value = false
    toast.ok('أُضيف المحامي')
    router.push(`/lawyers/${id}`)
  }
  catch (e) {
    toast.error(errorMessage(e, 'تعذر الحفظ'))
  }
  finally {
    saving.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="page">
    <div class="d-flex flex-wrap align-center justify-space-between ga-3 mb-4">
      <div>
        <h1 class="page-title">
          المحامون
        </h1>
        <div class="text-body-2 muted">
          لكل محامٍ محفظة تُموَّل من قاصة القانونية ويدفع منها رسوم المدينين. مجموع المحافظ:
          <strong class="num">{{ money(totalWallets) }}</strong>
        </div>
      </div>
      <div class="d-flex align-center ga-3">
        <VSwitch
          v-model="showInactive"
          label="إظهار غير الفعّالين"
          color="primary"
          density="compact"
          hide-details
        />
        <VBtn
          color="primary"
          variant="flat"
          prepend-icon="plus"
          @click="openNew"
        >
          محامٍ جديد
        </VBtn>
      </div>
    </div>

    <VProgressLinear
      v-if="loading"
      indeterminate
      color="primary"
      class="mb-3"
    />

    <VRow>
      <VCol
        v-for="lawyer in shown"
        :key="lawyer.id"
        cols="12"
        sm="6"
        lg="4"
      >
        <VCard
          class="pa-4 lawyer"
          :to="`/lawyers/${lawyer.id}`"
        >
          <div class="d-flex align-center ga-3 mb-3">
            <VAvatar
              color="primary"
              variant="tonal"
              rounded="lg"
            >
              <VIcon icon="briefcase" />
            </VAvatar>
            <div class="flex-grow-1">
              <div class="font-weight-bold">
                {{ lawyer.name }}
              </div>
              <div class="text-caption muted">
                {{ lawyer.branch || lawyer.phone || '—' }}
              </div>
            </div>
            <VChip
              v-if="!lawyer.isActive"
              size="small"
              variant="tonal"
            >
              غير فعّال
            </VChip>
          </div>
          <div class="d-flex justify-space-between">
            <div>
              <div class="text-caption muted">
                رصيد المحفظة
              </div>
              <div
                class="text-h6 font-weight-bold num"
                :class="lawyer.balance < 0 ? 'text-error' : ''"
              >
                {{ money(lawyer.balance) }}
              </div>
            </div>
            <div class="text-end">
              <div class="text-caption muted">
                مدينون مفتوحون
              </div>
              <div class="text-h6 font-weight-bold num">
                {{ lawyer.openCases }}
              </div>
            </div>
            <div class="text-end">
              <div class="text-caption muted">
                رسوم دفعها
              </div>
              <div class="text-h6 font-weight-bold num">
                {{ money(lawyer.feesPaid) }}
              </div>
            </div>
          </div>
        </VCard>
      </VCol>
      <VCol
        v-if="!loading && !shown.length"
        cols="12"
      >
        <VCard class="pa-10 text-center muted">
          لا يوجد محامون بعد. أضف أول محامٍ لتسند إليه المدينين.
        </VCard>
      </VCol>
    </VRow>

    <VDialog
      v-model="dialog"
      max-width="480"
    >
      <VCard>
        <VCardTitle class="pt-5 px-6">
          محامٍ جديد
        </VCardTitle>
        <VCardText class="px-6">
          <VTextField
            v-model="form.name"
            label="الاسم"
            autofocus
            class="mb-3"
            hide-details
          />
          <VTextField
            v-model="form.branch"
            label="الفرع"
            class="mb-3"
            hide-details
          />
          <VTextField
            v-model="form.phone"
            label="الهاتف"
            dir="ltr"
            class="mb-3"
            hide-details
          />
          <VTextarea
            v-model="form.notes"
            label="ملاحظات"
            rows="2"
            hide-details
          />
        </VCardText>
        <VCardActions class="px-6 pb-5">
          <VSpacer />
          <VBtn
            variant="text"
            @click="dialog = false"
          >
            إلغاء
          </VBtn>
          <VBtn
            color="primary"
            variant="flat"
            :loading="saving"
            @click="save"
          >
            حفظ
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>
  </div>
</template>

<style scoped>
.lawyer {
  transition: border-color 0.15s, box-shadow 0.15s;
}

.lawyer:hover {
  border-color: rgb(var(--v-theme-primary));
  box-shadow: 0 6px 18px rgba(47, 43, 61, 22%) !important;
}
</style>
