<script setup>
import { STAGES, money } from '../shared'

/**
 * The debtor path as a row of stations, right to left.
 * Board mode: `counts` given, stations are buttons and `modelValue` is the open stage.
 * Track mode: `current` given, stations before it are done and it is highlighted.
 */
const props = defineProps({
  modelValue: { type: String, default: '' },
  counts: { type: Object, default: null },
  current: { type: String, default: '' },
})

const emit = defineEmits(['update:modelValue'])

const track = () => props.counts === null
const currentIndex = () => props.current === 'Closed' ? STAGES.length : STAGES.findIndex(s => s.key === props.current)

function state(stage, index) {
  if (!track())
    return props.modelValue === stage.key ? 'active' : 'idle'
  if (index < currentIndex())
    return 'done'

  return index === currentIndex() ? 'active' : 'idle'
}
</script>

<template>
  <div
    class="flow"
    :class="{ 'flow--track': track() }"
  >
    <template
      v-for="(stage, index) in STAGES"
      :key="stage.key"
    >
      <component
        :is="track() ? 'div' : 'button'"
        :type="track() ? undefined : 'button'"
        class="flow__station"
        :class="`flow__station--${state(stage, index)}`"
        @click="!track() && emit('update:modelValue', stage.key)"
      >
        <div class="flow__head">
          <span class="flow__index">{{ index + 1 }}</span>
          <VIcon
            :icon="state(stage, index) === 'done' ? 'check' : stage.icon"
            size="20"
          />
        </div>
        <div class="flow__label">
          {{ stage.label }}
        </div>
        <template v-if="!track()">
          <div class="flow__count num">
            {{ counts[stage.key]?.count ?? 0 }}
          </div>
          <div class="flow__sub">
            <span class="num">{{ money(counts[stage.key]?.remainingSnapshot) }}</span> د.ع
          </div>
        </template>
      </component>
      <div
        v-if="index < STAGES.length - 1"
        class="flow__link"
        :class="{ 'flow__link--done': track() && index < currentIndex() }"
      >
        <VIcon
          icon="chevron-left"
          size="18"
        />
      </div>
    </template>
  </div>
</template>

<style scoped>
.flow {
  display: flex;
  align-items: stretch;
  overflow-x: auto;
  padding-block-end: 2px;
}

.flow__station {
  flex: 1 1 0;
  min-inline-size: 112px;
  padding: 14px 16px;
  border: 1px solid transparent;
  border-radius: 6px;
  box-shadow: 0 3px 12px rgba(47, 43, 61, 14%);
  background: rgb(var(--v-theme-surface));
  color: rgb(var(--v-theme-on-surface));
  font-family: inherit;
  text-align: start;
  transition: border-color 0.15s, background 0.15s, box-shadow 0.15s;
}

button.flow__station {
  cursor: pointer;
}

button.flow__station:hover {
  border-color: rgb(var(--v-theme-primary));
}

button.flow__station:focus-visible {
  outline: 2px solid rgb(var(--v-theme-accent));
  outline-offset: 2px;
}

.flow__station--active {
  border-color: rgb(var(--v-theme-primary));
  background: rgb(var(--v-theme-primary));
  box-shadow: 0 2px 6px rgba(var(--v-theme-primary), 0.3);
  color: #fff;
}

.flow__station--done {
  border-color: rgba(var(--v-theme-success), 0.5);
  background: rgba(var(--v-theme-success), 0.07);
  color: rgb(var(--v-theme-success));
}

.flow__head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  opacity: 0.85;
}

.flow__index {
  display: inline-grid;
  border: 1px solid currentcolor;
  border-radius: 50%;
  block-size: 22px;
  font-size: 0.75rem;
  font-weight: 700;
  inline-size: 22px;
  place-items: center;
}

.flow__label {
  margin-block-start: 8px;
  font-size: 0.92rem;
  font-weight: 700;
}

.flow__count {
  font-size: 1.9rem;
  font-weight: 700;
  line-height: 1.25;
  text-align: start;
}

.flow__sub {
  font-size: 0.78rem;
  opacity: 0.75;
}

.flow__link {
  display: grid;
  flex: 0 0 26px;
  color: rgba(var(--v-theme-on-surface), 0.35);
  place-items: center;
}

.flow__link--done {
  color: rgb(var(--v-theme-success));
}

.flow--track .flow__station {
  min-inline-size: 130px;
  padding: 10px 14px;
}

.flow--track .flow__label {
  margin-block-start: 4px;
  font-size: 0.85rem;
}
</style>
