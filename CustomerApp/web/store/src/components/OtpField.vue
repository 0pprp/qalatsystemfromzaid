<template>
  <div>
    <v-text-field
      ref="input"
      :model-value="modelValue"
      label="رمز التحقق"
      autocomplete="one-time-code"
      inputmode="numeric"
      pattern="[0-9]*"
      maxlength="6"
      dir="ltr"
      class="otp-field"
      :error-messages="errorMessages"
      :disabled="disabled"
      autofocus
      @update:model-value="onInput"
    />
    <div class="d-flex align-center justify-space-between text-body-2 mt-n2 mb-2">
      <span v-if="expiresLeft > 0" class="text-medium-emphasis">
        صلاحية الرمز: <span class="num ltr">{{ mmss(expiresLeft) }}</span>
      </span>
      <span v-else class="text-error">انتهت صلاحية الرمز، اطلب رمزاً جديداً</span>
      <v-btn variant="text" size="small" color="primary" :disabled="resendLeft > 0 || resending" :loading="resending" @click="$emit('resend')">
        <template v-if="resendLeft > 0">إعادة الإرسال بعد <span class="num ms-1 ltr">{{ resendLeft }}</span> ث</template>
        <template v-else>إعادة إرسال الرمز</template>
      </v-btn>
    </div>
  </div>
</template>

<script setup>
import { ref, watch, onBeforeUnmount } from 'vue'
import { toLatinDigits } from '@/utils/phone'

const props = defineProps({
  modelValue: { type: String, default: '' },
  expiresInSeconds: { type: Number, default: 300 },
  resendAfterSeconds: { type: Number, default: 60 },
  startedAt: { type: Number, default: 0 }, // Date.now() when the code was sent; change to restart timers
  errorMessages: { type: [String, Array], default: () => [] },
  disabled: Boolean,
  resending: Boolean,
})
const emit = defineEmits(['update:modelValue', 'complete', 'resend'])
const expiresLeft = ref(props.expiresInSeconds)
const resendLeft = ref(props.resendAfterSeconds)
let timer = null

function tick() {
  const elapsed = Math.floor((Date.now() - props.startedAt) / 1000)
  expiresLeft.value = Math.max(0, props.expiresInSeconds - elapsed)
  resendLeft.value = Math.max(0, props.resendAfterSeconds - elapsed)
}
watch(
  () => props.startedAt,
  () => {
    clearInterval(timer)
    tick()
    timer = setInterval(tick, 1000)
  },
  { immediate: true },
)
onBeforeUnmount(() => clearInterval(timer))

function onInput(v) {
  const digits = toLatinDigits(v ?? '').replace(/\D/g, '').slice(0, 6)
  emit('update:modelValue', digits)
  if (digits.length === 6) emit('complete', digits)
}
const mmss = (s) => `${String(Math.floor(s / 60)).padStart(2, '0')}:${String(s % 60).padStart(2, '0')}`
</script>

<style scoped>
.otp-field :deep(input) {
  text-align: center;
  font-size: 1.6rem;
  letter-spacing: 0.6em;
  font-weight: 700;
  padding-inline-start: 0.6em;
}
</style>
