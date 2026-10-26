import { describe, expect, it } from 'vitest'
import {
  indexEvaluationItems,
  lookupEvaluation,
  patchCategoryFromHits,
  categoryResultCount,
  categoryBlock,
} from '@/composables/salesRequestEvaluationMap'

describe('salesRequestEvaluationMap', () => {
  it('resolves evaluation when row cityValue differs from gateway sourceCityValue', () => {
    const items = [{
      requestId: 42,
      sourceCityValue: 'DatabaseCompanyBasra',
      key: 'DatabaseCompanyBasra:42',
      phone: { resultCount: 3, worstRatingLabel: 'متلكئ', worstScore: -5 },
      tripleName: { resultCount: 0, worstRatingLabel: 'لا توجد نتائج' },
      fatherGrandfather: { resultCount: 1, worstRatingLabel: 'ضعيف', worstScore: 0 },
      sonFather: { resultCount: 2, worstRatingLabel: 'جيد', worstScore: 5 },
    }]
    const rows = [{ id: 42, cityValue: 'البصرة', customerName: 'وائل' }]
    const map = indexEvaluationItems(items, rows)
    const found = lookupEvaluation(map, rows[0])
    expect(found).toBeTruthy()
    expect(categoryResultCount(categoryBlock(found, 'phone'))).toBe(3)
    expect(categoryResultCount(categoryBlock(found, 'fatherGrandfather'))).toBe(1)
    expect(categoryResultCount(categoryBlock(found, 'sonFather'))).toBe(2)
  })

  it('patches category counts from hits total', () => {
    const evalRow = {
      requestId: 1,
      phone: { resultCount: 0, worstRatingLabel: 'لا توجد نتائج' },
    }
    const patched = patchCategoryFromHits(evalRow, 'phone', 4, { ratingLabel: 'متلكئ', score: -5 })
    expect(categoryResultCount(categoryBlock(patched, 'phone'))).toBe(4)
    expect(categoryBlock(patched, 'phone').worstRatingLabel).toBe('متلكئ')
  })
})
