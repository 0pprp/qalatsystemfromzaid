<script setup>
import { useToast } from '@/composables/useToast'
import { computed } from 'vue'

const { dialogState, closeDialog, onConfirm } = useToast()

const isVisible = computed(() => dialogState.value.visible)

const iconData = computed(() => {
  switch (dialogState.value.type) {
  case 'success':
    return { icon: 'tabler-circle-check', color: 'success', animation: 'bounce-in' }
  case 'error':
    return { icon: 'tabler-alert-circle', color: 'error', animation: 'shake' }
  case 'warning':
    return { icon: 'tabler-alert-triangle', color: 'warning', animation: 'pulse' }
  case 'info':
  default:
    return { icon: 'tabler-info-circle', color: 'info', animation: 'fade-in' }
  }
})

function handleConfirm() {
  if (onConfirm.value) onConfirm.value()
  closeDialog()
}
</script>

<template>
  <VDialog
    v-model="isVisible"
    max-width="450"
    persistent
    transition="dialog-transition"
    class="global-message-dialog"
    style="z-index: 9999;"
  >
    <VCard class="text-center pa-6 rounded-xl elevation-10">
      <VCardText class="d-flex flex-column align-center">
        <!-- Animated Icon -->
        <div
          class="icon-wrapper mb-4"
          :class="iconData.animation"
        >
          <VIcon
            :icon="iconData.icon"
            size="80"
            :color="iconData.color"
          />
        </div>

        <!-- Title -->
        <h3 class="text-h4 font-weight-bold mb-2 text-wrap">
          {{ dialogState.title }}
        </h3>

        <!-- Message -->
        <p class="text-body-1 text-medium-emphasis mb-6 text-wrap">
          {{ dialogState.message }}
        </p>

        <!-- Buttons -->
        <div class="d-flex gap-4 w-100 justify-center">
          <VBtn
            v-if="dialogState.showCancel"
            variant="outlined"
            color="secondary"
            size="large"
            @click="closeDialog"
          >
            إلغاء
          </VBtn>
          
          <VBtn
            :color="iconData.color"
            size="large"
            variant="elevated"
            class="px-8"
            @click="handleConfirm"
          >
            موافق
          </VBtn>
        </div>
      </VCardText>
    </VCard>
  </VDialog>
</template>

<style scoped>
.global-message-dialog {
  backdrop-filter: blur(4px);
}

.icon-wrapper {
  display: flex;
  align-items: center;
  justify-content: center;
}

/* Animations */
@keyframes bounceIn {
  0% { opacity: 0; transform: scale(0.3); }
  50% { transform: scale(1.05); }
  70% { transform: scale(0.9); }
  100% { opacity: 1; transform: scale(1); }
}
.bounce-in { animation: bounceIn 0.6s cubic-bezier(0.215, 0.61, 0.355, 1) both; }

@keyframes shake {
  0%,
 100% { transform: translateX(0); }

  10%,
 30%,
 50%,
 70%,
 90% { transform: translateX(-5px); }

  20%,
 40%,
 60%,
 80% { transform: translateX(5px); }
}
.shake { animation: shake 0.5s both; }

@keyframes pulse {
  0% { transform: scale(1); }
  50% { transform: scale(1.1); }
  100% { transform: scale(1); }
}
.pulse { animation: pulse 0.8s infinite; }

@keyframes fadeIn {
  from { opacity: 0; transform: translateY(-10px); }
  to { opacity: 1; transform: translateY(0); }
}
.fade-in { animation: fadeIn 0.4s ease-out both; }
</style>
