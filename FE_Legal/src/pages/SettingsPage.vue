<script setup>
import { onMounted, ref } from 'vue'
import { api, errorMessage } from '../api'
import { money, toast } from '../shared'

const FLOWS = [
  { key: 'Judicial', label: 'خطوات الدعوى القضائية', icon: 'gavel' },
  { key: 'Execution', label: 'خطوات التنفيذ', icon: 'hammer' },
]

const AMOUNTS = [
  { key: 'JudicialFee', label: 'أجور اقامة الدعوى', hint: 'تُسجَّل عند نقل المدين إلى الدعوى القضائية' },
  { key: 'ExecutionFee', label: 'أجور التنفيذ', hint: 'تُسجَّل عند نقل المدين إلى التنفيذ' },
  { key: 'EarlySettlementPenalty', label: 'غرامة التسديد الكامل قبل الدعوى', hint: 'المبلغ المقترح في طلب التسديد الكامل' },
]

const stepTypes = ref([])
const settings = ref({})
const newName = ref({ Judicial: '', Execution: '' })
const newMandatory = ref({ Judicial: false, Execution: false })
const amountDraft = ref({})
const saving = ref(false)

const typesOf = flow => stepTypes.value.filter(t => t.flow === flow)

async function load() {
  try {
    const [types, values] = await Promise.all([api.stepTypes(), api.settings()])

    stepTypes.value = types
    settings.value = values
    amountDraft.value = { ...values }
  }
  catch (e) {
    toast.error(errorMessage(e, 'تعذر تحميل الإعدادات'))
  }
}

async function add(flow) {
  const name = newName.value[flow].trim()
  if (!name)
    return
  try {
    await api.saveStepType({ flow, name, sortOrder: 0, isActive: true, isMandatory: newMandatory.value[flow] })
    newName.value[flow] = ''
    newMandatory.value[flow] = false
    await load()
  }
  catch (e) {
    toast.error(errorMessage(e, 'تعذر إضافة الخطوة'))
  }
}

async function save(type, patch) {
  try {
    await api.saveStepType({ id: type.id, flow: type.flow, name: type.name, sortOrder: type.sortOrder, isActive: type.isActive, isMandatory: type.isMandatory, ...patch })
    await load()
  }
  catch (e) {
    toast.error(errorMessage(e, 'تعذر الحفظ'))
  }
}

async function setMandatory(type, isMandatory) {
  await save(type, { isMandatory })
  toast.ok(isMandatory ? `«${type.name}» أصبحت إلزامية` : `«${type.name}» أصبحت اختيارية`)
}

function rename(type) {
  const name = window.prompt('اسم الخطوة', type.name)
  if (name && name.trim() && name.trim() !== type.name)
    save(type, { name: name.trim() })
}

async function remove(type) {
  const used = type.usedCount
    ? `\nسُجّلت على المدينين ${type.usedCount} مرة؛ تبقى في ملفاتهم كما هي.`
    : ''
  const mandatory = type.isMandatory ? '\nهي خطوة إلزامية: بعد الحذف لن تُشترط على أحد.' : ''
  if (!window.confirm(`حذف الخطوة «${type.name}» من القائمة؟${used}${mandatory}`))
    return
  try {
    await api.deleteStepType(type.id)
    toast.ok(`حُذفت «${type.name}»`)
    await load()
  }
  catch (e) {
    toast.error(errorMessage(e, 'تعذر الحذف'))
  }
}

/** Swaps the order with the neighbour above/below. */
async function shift(type, direction) {
  const list = typesOf(type.flow)
  const other = list[list.indexOf(type) + direction]
  if (!other)
    return
  saving.value = true
  try {
    const [a, b] = [type.sortOrder, other.sortOrder]

    await api.saveStepType({ id: type.id, flow: type.flow, name: type.name, isActive: type.isActive, isMandatory: type.isMandatory, sortOrder: a === b ? b + direction : b })
    await api.saveStepType({ id: other.id, flow: other.flow, name: other.name, isActive: other.isActive, isMandatory: other.isMandatory, sortOrder: a })
    await load()
  }
  catch (e) {
    toast.error(errorMessage(e, 'تعذر تغيير الترتيب'))
  }
  finally {
    saving.value = false
  }
}

async function saveAmount(key) {
  const value = Number(amountDraft.value[key])
  if (!Number.isFinite(value) || value < 0) {
    toast.error('مبلغ غير صحيح')

    return
  }
  try {
    settings.value = await api.setSetting(key, Math.round(value))
    amountDraft.value = { ...settings.value }
    toast.ok('حُفظ المبلغ — يُطبَّق على الحركات القادمة فقط')
  }
  catch (e) {
    toast.error(errorMessage(e, 'تعذر الحفظ'))
  }
}

onMounted(load)
</script>

<template>
  <div class="page">
    <h1 class="page-title mb-1">
      الإعدادات
    </h1>
    <div class="text-body-2 muted mb-4">
      خطوات الدعوى والتنفيذ ومبالغ الأجور. الخطوة الإلزامية يجب تسجيلها على المدين قبل نقله من الدعوى القضائية إلى القرارات.
    </div>

    <VRow>
      <VCol
        v-for="flow in FLOWS"
        :key="flow.key"
        cols="12"
        md="6"
      >
        <VCard>
          <div class="d-flex align-center ga-2 pa-4 pb-2">
            <VIcon
              :icon="flow.icon"
              color="primary"
            />
            <div class="section-title">
              {{ flow.label }}
            </div>
          </div>
          <VList
            density="compact"
            class="py-0"
          >
            <VListItem
              v-for="(type, index) in typesOf(flow.key)"
              :key="type.id"
              :class="{ 'step-off': !type.isActive }"
            >
              <template #prepend>
                <span class="step-order num">{{ index + 1 }}</span>
              </template>
              <VListItemTitle>
                {{ type.name }}
                <VChip
                  v-if="type.isMandatory"
                  size="x-small"
                  color="error"
                  variant="tonal"
                  class="ms-1"
                >
                  إلزامية
                </VChip>
              </VListItemTitle>
              <VListItemSubtitle v-if="type.usedCount">
                استُخدمت {{ type.usedCount }} مرة
              </VListItemSubtitle>
              <template #append>
                <VBtn
                  icon="arrow-up"
                  size="x-small"
                  variant="text"
                  :disabled="saving || index === 0"
                  title="تقديم"
                  @click="shift(type, -1)"
                />
                <VBtn
                  icon="arrow-down"
                  size="x-small"
                  variant="text"
                  :disabled="saving || index === typesOf(flow.key).length - 1"
                  title="تأخير"
                  @click="shift(type, 1)"
                />
                <VBtn
                  icon="edit"
                  size="x-small"
                  variant="text"
                  title="تغيير الاسم"
                  @click="rename(type)"
                />
                <VBtn
                  icon="trash"
                  size="x-small"
                  variant="text"
                  color="error"
                  title="حذف الخطوة"
                  @click="remove(type)"
                />
                <VSwitch
                  :model-value="type.isMandatory"
                  label="إلزامية"
                  color="error"
                  density="compact"
                  hide-details
                  class="ms-3"
                  @update:model-value="setMandatory(type, $event)"
                />
                <VSwitch
                  :model-value="type.isActive"
                  label="فعّالة"
                  color="primary"
                  density="compact"
                  hide-details
                  class="ms-3"
                  @update:model-value="save(type, { isActive: $event })"
                />
              </template>
            </VListItem>
          </VList>
          <VDivider />
          <div class="d-flex align-center ga-2 pa-4">
            <VTextField
              v-model="newName[flow.key]"
              placeholder="اسم خطوة جديدة"
              density="compact"
              hide-details
              @keyup.enter="add(flow.key)"
            />
            <VSwitch
              v-model="newMandatory[flow.key]"
              label="إلزامية"
              color="error"
              density="compact"
              hide-details
              class="flex-grow-0"
            />
            <VBtn
              color="primary"
              variant="flat"
              prepend-icon="plus"
              :disabled="!newName[flow.key].trim()"
              @click="add(flow.key)"
            >
              إضافة
            </VBtn>
          </div>
        </VCard>
      </VCol>
    </VRow>

    <VCard class="mt-4">
      <div class="section-title pa-4 pb-0">
        المبالغ
      </div>
      <div class="text-body-2 muted px-4 pb-2">
        تغيير المبلغ لا يعدّل ما سُجّل سابقاً.
      </div>
      <VRow class="pa-4 pt-2">
        <VCol
          v-for="item in AMOUNTS"
          :key="item.key"
          cols="12"
          md="4"
        >
          <VTextField
            v-model.number="amountDraft[item.key]"
            type="number"
            min="0"
            :label="item.label"
            :hint="`${item.hint} — الحالي ${money(settings[item.key])}`"
            persistent-hint
            dir="ltr"
          >
            <template #append>
              <VBtn
                variant="tonal"
                :disabled="Number(amountDraft[item.key]) === Number(settings[item.key])"
                @click="saveAmount(item.key)"
              >
                حفظ
              </VBtn>
            </template>
          </VTextField>
        </VCol>
      </VRow>
    </VCard>
  </div>
</template>

<style scoped>
.step-order {
  display: inline-grid;
  border-radius: 50%;
  background: rgba(var(--v-theme-primary), 0.1);
  block-size: 24px;
  color: rgb(var(--v-theme-primary));
  font-size: 0.78rem;
  font-weight: 700;
  inline-size: 24px;
  margin-inline-end: 12px;
  place-items: center;
}

.step-off {
  opacity: 0.5;
}
</style>
