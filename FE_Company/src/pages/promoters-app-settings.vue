<script setup>
import { onMounted, ref } from 'vue'
import {
  paBroadcast,
  paDateTime,
  paErrorMessage,
  paFieldErrors,
  paGetSettings,
  paListBroadcasts,
  paPaged,
  paSaveSettings,
} from '@/composables/promotersAppApi'
import { useToast } from '@/composables/useToast'

definePage({ meta: { layout: 'default' } })

const toast = useToast()
const settings = ref({ supportPhone: '', supportWhatsapp: '', minPayout: 5000 })
const saving = ref(false)
const errors = ref({})
const loadError = ref('')

const msg = ref({ title: '', body: '' })
const sendingMsg = ref(false)
const msgErrors = ref({})
const broadcasts = ref([])

async function load() {
  loadError.value = ''
  try {
    const [s, b] = await Promise.all([paGetSettings(), paListBroadcasts({ pageSize: 10 })])

    settings.value = s
    broadcasts.value = paPaged(b).items
  }
  catch (err) {
    loadError.value = paErrorMessage(err, 'تعذر تحميل الإعدادات')
  }
}

async function save() {
  saving.value = true
  errors.value = {}
  try {
    settings.value = await paSaveSettings({ ...settings.value, minPayout: Number(settings.value.minPayout) || 0 })
    toast.success('تم حفظ الإعدادات')
  }
  catch (err) {
    errors.value = paFieldErrors(err)
    toast.error(paErrorMessage(err))
  }
  finally {
    saving.value = false
  }
}

async function sendMsg() {
  sendingMsg.value = true
  msgErrors.value = {}
  try {
    const r = await paBroadcast(msg.value)

    toast.success(`أُرسلت الرسالة إلى ${r.recipients} مروّج`)
    msg.value = { title: '', body: '' }
    load()
  }
  catch (err) {
    msgErrors.value = paFieldErrors(err)
    toast.error(paErrorMessage(err))
  }
  finally {
    sendingMsg.value = false
  }
}

const e = (bag, k) => (bag.value?.[k] ? [bag.value[k]] : [])

onMounted(load)
</script>

<template>
  <div>
    <h4 class="text-h4 mb-1 d-flex align-center gap-2">
      <VIcon
        icon="tabler-settings"
        color="primary"
      />
      إعدادات تطبيق المروّجين
    </h4>
    <p class="text-medium-emphasis mb-4">
      إعدادات خاصة بتطبيق المروّجين أونلاين (منفصلة عن تطبيق المسوّقين).
    </p>

    <VAlert
      v-if="loadError"
      type="error"
      variant="tonal"
      class="mb-4"
    >
      {{ loadError }}
    </VAlert>

    <VRow>
      <VCol
        cols="12"
        md="5"
      >
        <VCard title="عام">
          <VCardText>
            <VTextField
              v-model="settings.supportPhone"
              label="رقم الدعم (اتصال)"
              dir="ltr"
              class="mb-3"
              :error-messages="e(errors, 'supportPhone')"
            />
            <VTextField
              v-model="settings.supportWhatsapp"
              label="رقم الدعم (واتساب)"
              dir="ltr"
              class="mb-3"
              :error-messages="e(errors, 'supportWhatsapp')"
            />
            <VTextField
              v-model="settings.minPayout"
              type="number"
              label="أقل مبلغ للسحب (د.ع)"
              dir="ltr"
              :error-messages="e(errors, 'minPayout')"
            />
          </VCardText>
          <VCardActions>
            <VSpacer />
            <VBtn
              color="primary"
              :loading="saving"
              @click="save"
            >
              حفظ
            </VBtn>
          </VCardActions>
        </VCard>
      </VCol>
      <VCol
        cols="12"
        md="7"
      >
        <VCard title="رسالة لكل المروّجين">
          <VCardText>
            <VTextField
              v-model="msg.title"
              label="العنوان"
              class="mb-3"
              :error-messages="e(msgErrors, 'title')"
            />
            <VTextarea
              v-model="msg.body"
              label="النص"
              rows="3"
              auto-grow
              :error-messages="e(msgErrors, 'body')"
            />
            <div class="text-caption text-medium-emphasis mt-1">
              تظهر في إشعارات التطبيق.
            </div>
          </VCardText>
          <VCardActions>
            <VSpacer />
            <VBtn
              color="primary"
              prepend-icon="tabler-send"
              :loading="sendingMsg"
              @click="sendMsg"
            >
              إرسال
            </VBtn>
          </VCardActions>
          <VDivider />
          <VList density="compact">
            <VListItem
              v-for="b in broadcasts"
              :key="b.id"
              :title="b.title"
              :subtitle="`${paDateTime(b.createdAt)} · ${b.createdBy} · ${b.recipients} مروّج`"
            />
            <VListItem
              v-if="!broadcasts.length"
              subtitle="لم تُرسل رسائل بعد"
            />
          </VList>
        </VCard>
      </VCol>
    </VRow>
  </div>
</template>
