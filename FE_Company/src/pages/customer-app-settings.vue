<script setup>
import { computed, onMounted, ref, watch } from 'vue'
import {
  caBroadcast,
  caDateTime,
  caErrorMessage,
  caFieldErrors,
  caGetSettings,
  caHoursToMinutes,
  caListBroadcasts,
  caMinutesToHours,
  caPaged,
  caUpdateSettings,
} from '@/composables/customerAppApi'
import { useToast } from '@/composables/useToast'

definePage({ meta: { layout: 'default' } })

const toast = useToast()

// ---------------------------------------------------------------- settings
const form = ref(null)
const loading = ref(false)
const saving = ref(false)
const loadError = ref('')
const fieldErrors = ref({})

async function load() {
  loading.value = true
  loadError.value = ''
  try {
    form.value = await caGetSettings()
  }
  catch (err) {
    loadError.value = caErrorMessage(err, 'تعذر تحميل الإعدادات')
  }
  finally {
    loading.value = false
  }
}

async function save() {
  saving.value = true
  fieldErrors.value = {}
  try {
    form.value = await caUpdateSettings(form.value)
    toast.success('تم حفظ الإعدادات')
  }
  catch (err) {
    fieldErrors.value = caFieldErrors(err)
    toast.error(caErrorMessage(err, 'تعذر حفظ الإعدادات'))
  }
  finally {
    saving.value = false
  }
}

const err = key => fieldErrors.value?.[key]

// The target is stored in minutes; the field is in hours.
const targetHours = computed({
  get: () => caMinutesToHours(form.value?.dailyWorkMinutesTarget ?? 0),
  set: v => { if (form.value) form.value.dailyWorkMinutesTarget = caHoursToMinutes(v) },
})

// ---------------------------------------------------------------- notifications to agents
const message = ref({ title: '', body: '' })
const sending = ref(false)
const confirmSend = ref(false)

const history = ref([])
const historyTotal = ref(0)
const historyPage = ref(1)
const historyPageSize = ref(10)
const historyLoading = ref(false)

const historyHeaders = [
  { title: 'الإشعار', key: 'title', sortable: false },
  { title: 'وصل إلى', key: 'recipients', sortable: false, align: 'center' },
  { title: 'أرسله', key: 'createdBy', sortable: false },
  { title: 'التاريخ', key: 'createdAt', sortable: false },
]

async function loadHistory() {
  historyLoading.value = true
  try {
    const res = caPaged(await caListBroadcasts({ page: historyPage.value, pageSize: historyPageSize.value }))

    history.value = res.items
    historyTotal.value = res.total
  }
  catch {
    history.value = []
    historyTotal.value = 0
  }
  finally {
    historyLoading.value = false
  }
}
watch([historyPage, historyPageSize], loadHistory)

async function send() {
  sending.value = true
  try {
    const res = await caBroadcast({ title: message.value.title, body: message.value.body })

    toast.success(`تم إرسال الإشعار إلى ${res?.recipients ?? 0} مسوّق`)
    message.value = { title: '', body: '' }
    confirmSend.value = false
    loadHistory()
  }
  catch (e) {
    toast.error(caErrorMessage(e, 'تعذر إرسال الإشعار'))
  }
  finally {
    sending.value = false
  }
}

onMounted(() => {
  load()
  loadHistory()
})
</script>

<template>
  <div>
    <div class="mb-4">
      <h4 class="text-h4 mb-1 d-flex align-center gap-2">
        <VIcon
          icon="tabler-settings"
          color="primary"
        />
        إعدادات التطبيق
      </h4>
      <p class="text-medium-emphasis mb-0">
        أرقام الدعم التي تظهر للمسوّقين والإشعارات المرسلة لهم. إصدار التطبيق يحدده آخر ملف APK منشور، والتحديث إليه إلزامي.
      </p>
    </div>

    <VAlert
      v-if="loadError"
      type="error"
      variant="tonal"
      class="mb-4"
    >
      {{ loadError }}
      <template #append>
        <VBtn
          variant="text"
          @click="load"
        >
          إعادة المحاولة
        </VBtn>
      </template>
    </VAlert>

    <VRow>
      <VCol
        cols="12"
        lg="6"
      >
        <VCard
          :loading="loading"
          class="mb-4"
        >
          <VCardItem
            title="الدعم"
            subtitle="يظهر في التطبيق ضمن «حسابي» ليتصل المسوّق بالإدارة"
          >
            <template #prepend>
              <VIcon
                icon="tabler-headset"
                color="primary"
              />
            </template>
          </VCardItem>
          <VCardText v-if="form">
            <VRow dense>
              <VCol
                cols="12"
                md="6"
              >
                <VTextField
                  v-model="form.supportPhone"
                  label="رقم الاتصال"
                  placeholder="07XXXXXXXXX"
                  dir="ltr"
                  prepend-inner-icon="tabler-phone"
                  :error-messages="err('supportPhone')"
                />
              </VCol>
              <VCol
                cols="12"
                md="6"
              >
                <VTextField
                  v-model="form.supportWhatsapp"
                  label="رقم واتساب"
                  placeholder="9647XXXXXXXXX"
                  dir="ltr"
                  prepend-inner-icon="tabler-brand-whatsapp"
                  :error-messages="err('supportWhatsapp')"
                />
              </VCol>
              <VCol cols="12">
                <VTextField
                  v-model.number="targetHours"
                  type="number"
                  min="0"
                  max="24"
                  step="0.5"
                  label="الهدف اليومي لوقت العمل (لكل مسوّق)"
                  hint="ساعات العمل المطلوبة يومياً، محسوبة من فترات تثبيت المحلات فقط. لا يراه المسوّق. 0 = بدون هدف."
                  persistent-hint
                  prepend-inner-icon="tabler-target-arrow"
                  suffix="ساعة"
                  :error-messages="err('dailyWorkMinutesTarget')"
                />
              </VCol>
            </VRow>
          </VCardText>
          <VDivider />
          <VCardActions class="pa-4">
            <VSpacer />
            <VBtn
              color="primary"
              variant="flat"
              prepend-icon="tabler-device-floppy"
              :loading="saving"
              :disabled="!form"
              @click="save"
            >
              حفظ الإعدادات
            </VBtn>
          </VCardActions>
        </VCard>
      </VCol>

      <VCol
        cols="12"
        lg="6"
      >
        <VCard class="mb-4">
          <VCardItem
            title="إشعار للمسوّقين"
            subtitle="يصل إلى جميع المسوّقين النشطين داخل التطبيق وكإشعار على الهاتف"
          >
            <template #prepend>
              <VIcon
                icon="tabler-speakerphone"
                color="primary"
              />
            </template>
          </VCardItem>
          <VCardText>
            <VTextField
              v-model="message.title"
              label="العنوان"
              counter="150"
              maxlength="150"
              class="mb-3"
            />
            <VTextarea
              v-model="message.body"
              label="النص"
              rows="3"
              auto-grow
              counter="1000"
              maxlength="1000"
            />
          </VCardText>
          <VCardActions class="pa-4 pt-0">
            <VSpacer />
            <VBtn
              color="primary"
              variant="flat"
              prepend-icon="tabler-send"
              :disabled="!message.title.trim() || !message.body.trim()"
              @click="confirmSend = true"
            >
              إرسال
            </VBtn>
          </VCardActions>
        </VCard>

        <VCard>
          <VCardItem title="الإشعارات المرسلة" />
          <VDataTableServer
            v-model:page="historyPage"
            v-model:items-per-page="historyPageSize"
            :headers="historyHeaders"
            :items="history"
            :items-length="historyTotal"
            :loading="historyLoading"
            item-value="id"
            density="comfortable"
            no-data-text="لم يُرسل أي إشعار بعد"
          >
            <template #item.title="{ item }">
              <div class="font-weight-medium">
                {{ item.title }}
              </div>
              <div class="text-caption text-medium-emphasis ca-clamp">
                {{ item.body }}
              </div>
            </template>
            <template #item.createdAt="{ item }">
              <span class="text-no-wrap">{{ caDateTime(item.createdAt) }}</span>
            </template>
          </VDataTableServer>
        </VCard>
      </VCol>
    </VRow>

    <VDialog
      v-model="confirmSend"
      max-width="460"
    >
      <VCard>
        <VCardTitle class="pa-4">
          إرسال الإشعار؟
        </VCardTitle>
        <VCardText>
          سيصل «<strong>{{ message.title }}</strong>» إلى جميع المسوّقين النشطين ولا يمكن التراجع عنه.
        </VCardText>
        <VCardActions class="pa-4">
          <VSpacer />
          <VBtn
            variant="text"
            :disabled="sending"
            @click="confirmSend = false"
          >
            تراجع
          </VBtn>
          <VBtn
            color="primary"
            variant="flat"
            :loading="sending"
            @click="send"
          >
            إرسال
          </VBtn>
        </VCardActions>
      </VCard>
    </VDialog>
  </div>
</template>

<style scoped>
.ca-clamp {
  display: -webkit-box;
  overflow: hidden;
  -webkit-box-orient: vertical;
  -webkit-line-clamp: 2;
}
</style>
