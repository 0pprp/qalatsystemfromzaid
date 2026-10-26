import { computed } from 'vue'

/** True for production short/summary builds (see .env.production). */
export const isSummaryReadOnly = String(import.meta.env.VITE_SUMMARY_READONLY || 'true').toLowerCase() !== 'false'

export function useSummaryReadOnly() {
  const canMutate = computed(() => !isSummaryReadOnly)

  return {
    isSummaryReadOnly,
    canMutate,
  }
}
