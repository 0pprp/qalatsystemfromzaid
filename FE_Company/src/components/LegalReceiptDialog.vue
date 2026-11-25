<script setup>
import { onBeforeUnmount, ref, watch } from 'vue'
import { useToast } from '@/composables/useToast'
import { legalError, legalReceiptPdf } from '@/composables/legalDeskApi'

/** Shows the receipt of one تسديد (PDF) with buttons to download it or open it for printing. */
const props = defineProps({
  modelValue: { type: Boolean, default: false },
  paymentId: { type: Number, default: 0 },
  receiptNumber: { type: [Number, String], default: '' },
})

const emit = defineEmits(['update:modelValue'])

const toast = useToast()
const loading = ref(false)
const url = ref('')

function release() {
  if (url.value)
    URL.revokeObjectURL(url.value)
  url.value = ''
}

async function load() {
  release()
  loading.value = true
  try {
    url.value = URL.createObjectURL(await legalReceiptPdf(props.paymentId))
  }
  catch (e) {
    toast.error(legalError(e, 'تعذر تحميل الوصل'))
    emit('update:modelValue', false)
  }
  finally {
    loading.value = false
  }
}

watch(() => [props.modelValue, props.paymentId], ([shown, id]) => {
  if (shown && id)
    load()
  else if (!shown)
    release()
})

onBeforeUnmount(release)

function download() {
  const link = document.createElement('a')

  link.href = url.value
  link.download = `وصل-${props.receiptNumber || props.paymentId}.pdf`
  link.click()
}

const openInTab = () => window.open(url.value, '_blank')
</script>

<template>
  <VDialog
    :model-value="modelValue"
    max-width="620"
    @update:model-value="emit('update:modelValue', $event)"
  >
    <VCard>
      <VCardTitle class="d-flex align-center justify-space-between">
        <span>وصل تسديد <span dir="ltr">{{ receiptNumber }}</span></span>
        <div class="d-flex gap-2">
          <VBtn
            size="small"
            variant="tonal"
            prepend-icon="tabler-printer"
            :disabled="!url"
            @click="openInTab"
          >
            فتح للطباعة
          </VBtn>
          <VBtn
            size="small"
            color="primary"
            prepend-icon="tabler-download"
            :disabled="!url"
            @click="download"
          >
            تحميل PDF
          </VBtn>
        </div>
      </VCardTitle>
      <VProgressLinear
        v-if="loading"
        indeterminate
      />
      <iframe
        v-if="url"
        :src="`${url}#navpanes=0&view=FitH`"
        title="وصل تسديد"
        style="border: 0; block-size: 70vh; inline-size: 100%;"
      />
      <VCardActions>
        <VSpacer />
        <VBtn
          variant="text"
          @click="emit('update:modelValue', false)"
        >
          إغلاق
        </VBtn>
      </VCardActions>
    </VCard>
  </VDialog>
</template>
