import assert from 'node:assert/strict'
import test from 'node:test'
import {
  buildEvaluationKey,
  indexEvaluationItems,
  lookupEvaluation,
} from './salesRequestEvaluationMap.js'

test('index+lookup keep evaluations isolated per city for same request id', () => {
  const najafRow = { id: 5, cityValue: 'DatabaseCompanyNajaf', customerName: 'A' }
  const karbalaRow = { id: 5, cityValue: 'DatabaseCompanyKarbala', customerName: 'B' }
  const najafEval = {
    requestId: 5,
    sourceCityValue: 'DatabaseCompanyNajaf',
    phone: { resultCount: 2, worstRatingLabel: 'ممتاز' },
  }
  const karbalaEval = {
    requestId: 5,
    sourceCityValue: 'DatabaseCompanyKarbala',
    phone: { resultCount: 9, worstRatingLabel: 'سيء' },
  }

  const map = {
    ...indexEvaluationItems([najafEval], [najafRow]),
    ...indexEvaluationItems([karbalaEval], [karbalaRow]),
  }

  const forNajaf = lookupEvaluation(map, najafRow)
  const forKarbala = lookupEvaluation(map, karbalaRow)

  assert.equal(forNajaf?.phone?.resultCount ?? forNajaf?.phone?.ResultCount, 2)
  assert.equal(forKarbala?.phone?.resultCount ?? forKarbala?.phone?.ResultCount, 9)
  assert.notEqual(forNajaf, forKarbala)
})

test('lookup does not fall back to another city by bare request id', () => {
  const map = indexEvaluationItems([{
    requestId: 42,
    sourceCityValue: 'DatabaseCompanyNajaf',
    key: buildEvaluationKey('DatabaseCompanyNajaf', 42),
    phone: { resultCount: 3, worstRatingLabel: 'جيد' },
  }], [{ id: 42, cityValue: 'DatabaseCompanyNajaf' }])

  const hit = lookupEvaluation(map, { id: 42, cityValue: 'DatabaseCompanyKarbala' })
  assert.equal(hit, null)
})
