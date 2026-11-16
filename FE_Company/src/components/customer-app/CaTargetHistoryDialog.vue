<script setup>
import { computed, ref, watch } from 'vue'
import {
  CA_TARGET_STATUS,
  caAgentTargets,
  caDate,
  caErrorMessage,
  caFieldErrors,
  caHoursToMinutes,
  caMinutes,
  caMinutesToHours,
  caSetAgentTarget,
} from '@/composables/customerAppApi'
import { useToast } from '@/composables/useToast'

/**
 * «سجل الهدف اليومي» of one agent: work time per day against the time target (last 90 days), and their own target.
 * Emits `changed` after the agent's own target is saved.
 */
const props = defineProps({
  agent: { type: Object, default: null }, // { id, fullName }
})

const emit = defineEmits(['close', 'changed'])
const toast = useToast()

const open = computed({
  get: () => !!props.agent,
  set: v => { if (!v) emit('close') },
})

const loading = ref(false)
const error = ref('')
const data = ref(null)
const filter = ref('all')

const useOwn = ref(false)
const own = ref(null)
const saving = ref(false)
const fieldError = ref('')

async function load() {
  if (!props.agent) return
  loading.value = true
  error.value = ''
  try {
    data.value = await caAgentTargets(props.agent.id)
    useOwn.value = data.value.ownTargetMinutes != null
    own.value = caMinutesToHours(data.value.ownTargetMinutes ?? data.value.currentTargetMinutes)
  }
  catch (e) {
    error.value = caErrorMessage(e, 'تعذر تحميل سجل الهدف')
  }
  finally {
    loading.value = false
  }
}

watch(() => props.agent?.id, id => {
  data.value = null
  filter.value = 'all'
  fieldError.value = ''
  if (id) load()
}, { immediate: true })

const days = computed(() => (data.value?.days || []).filter(d => filter.value === 'all' || d.status === filter.value))

async function saveTarget() {
  saving.value = true
  fieldError.value = ''
  try {
    const value = useOwn.value ? caHoursToMinutes(own.value) : null

    data.value = await caSetAgentTarget(props.agent.id, value)
    useOwn.value = data.value.ownTargetMinutes != null
    own.value = caMinutesToHours(data.value.ownTargetMinutes ?? data.value.currentTargetMinutes)
    toast.success(useOwn.value ? 'تم حفظ هدف المسوّق' : 'المسوّق يتبع الهدف العام الآن')
    emit('changed')
  }
  catch (e) {
    fieldError.value = caFieldErrors(e)?.dailyWorkMinutesTarget || ''
    toast.error(caErrorMessage(e, 'تعذر حفظ الهدف'))
  }
  finally {
    saving.value = false
  }
}
</script>

<template>
  <VDialog
    v-model="open"
    max-width="720"
    scrollable
  >
    <VCard v-if="agent">
      <VCardItem>
        <VCardTitle class="d-flex align-center gap-2">
          <VIcon
            icon="tabler-target-arrow"
            color="primary"
          />
          سجل الهدف اليومي — {{ agent.fullName }}
        </VCardTitle>
        <template #append>
          <IconBtn @click="open = false">
            <VIcon icon="tabler-x" />
          </IconBtn>
        </template>
      </VCardItem>

      <VCardText>
        <VAlert
          v-if="error"
          type="error"
          variant="tonal"
          class="mb-4"
        >
          {{ error }}
        </VAlert>
        <VProgressLinear
          v-if="loading"
          indeterminate
          color="primary"
          class="mb-4"
        />

        <template v-if="data">
          <!-- Summary -->
          <VRow
            dense
            class="mb-2"
          >
            <VCol
              v-for="k in ['met', 'missed', 'off']"
              :key="k"
              cols="4"
            >
              <VCard
                variant="tonal"
                :color="CA_TARGET_STATUS[k].color"
                class="pa-3 text-center ca-t-stat"
                :class="{ 'is-active': filter === k }"
                @click="filter = filter === k ? 'all' : k"
              >
                <div class="text-h4 font-weight-bold">
                  {{ k === 'met' ? data.metDays : k === 'missed' ? data.missedDays : data.offDays }}
                </div>
                <div class="text-body-2">
                  {{ k === 'off' ? 'يوم لم يعمل' : k === 'met' ? 'يوم حقق الهدف' : 'يوم لم يحقق الهدف' }}
                </div>
              </VCard>
            </VCol>
          </VRow>
          <div class="text-caption text-medium-emphasis mb-4">
            آخر 90 يوماً. اليوم الحالي لا يُحتسب حتى ينتهي. اضغط على رقم لعرض تلك الأيام فقط.
          </div>

          <!-- Own target -->
          <VCard
            variant="outlined"
            class="pa-3 mb-4"
          >
            <div class="d-flex flex-wrap align-center gap-3">
              <div class="flex-grow-1">
                <div class="font-weight-medium">
                  هدفه الحالي: {{ data.currentTargetMinutes > 0 ? `${caMinutes(data.currentTargetMinutes)} عمل يومياً` : 'بدون هدف' }}
                </div>
                <div class="text-caption text-medium-emphasis">
                  {{ data.ownTargetMinutes != null ? 'هدف خاص بهذا المسوّق' : 'يتبع الهدف العام من «إعدادات التطبيق»' }}
                </div>
              </div>
              <VSwitch
                v-model="useOwn"
                label="هدف خاص"
                hide-details
                density="compact"
              />
              <VTextField
                v-if="useOwn"
                v-model.number="own"
                type="number"
                min="0"
                max="24"
                step="0.5"
                label="ساعة يومياً"
                density="compact"
                style="max-inline-size: 130px;"
                :error-messages="fieldError"
                hide-details="auto"
              />
              <VBtn
                color="primary"
                variant="flat"
                :loading="saving"
                @click="saveTarget"
              >
                حفظ
              </VBtn>
            </div>
          </VCard>

          <!-- Days -->
          <VTable density="comfortable">
            <thead>
              <tr>
                <th>اليوم</th>
                <th>وقت العمل</th>
                <th class="text-center">
                  المحلات
                </th>
                <th>النتيجة</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="d in days"
                :key="d.day"
              >
                <td class="text-no-wrap">
                  {{ caDate(d.day) }}
                  <VChip
                    v-if="!d.final"
                    size="x-small"
                    variant="tonal"
                    class="ms-1"
                  >
                    مؤقت
                  </VChip>
                </td>
                <td class="text-no-wrap">
                  <b>{{ caMinutes(d.workMinutes) }}</b>
                  <span
                    v-if="d.targetMinutes > 0"
                    class="text-caption text-medium-emphasis"
                  > من {{ caMinutes(d.targetMinutes) }}</span>
                </td>
                <td class="text-center">
                  {{ d.pins }}
                </td>
                <td>
                  <VChip
                    size="small"
                    :color="CA_TARGET_STATUS[d.status].color"
                    :prepend-icon="CA_TARGET_STATUS[d.status].icon"
                    variant="tonal"
                  >
                    {{ CA_TARGET_STATUS[d.status].title }}
                  </VChip>
                </td>
              </tr>
              <tr v-if="!days.length">
                <td
                  colspan="4"
                  class="text-center text-medium-emphasis py-6"
                >
                  {{ data.days.length ? 'لا توجد أيام بهذه النتيجة' : 'لا يوجد سجل بعد. يبدأ السجل من يوم تفعيل الهدف.' }}
                </td>
              </tr>
            </tbody>
          </VTable>
        </template>
      </VCardText>
    </VCard>
  </VDialog>
</template>

<style scoped>
.ca-t-stat {
  cursor: pointer;
}

.ca-t-stat.is-active {
  outline: 2px solid currentcolor;
}
</style>
