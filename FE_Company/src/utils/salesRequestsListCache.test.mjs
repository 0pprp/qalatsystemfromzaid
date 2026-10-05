import assert from 'node:assert/strict'
import test from 'node:test'
import {
  capEvaluationRows,
  cityCacheKey,
  createCityListCache,
} from './salesRequestsListCache.js'

test('cityCacheKey normalizes empty to __all__', () => {
  assert.equal(cityCacheKey(''), '__all__')
  assert.equal(cityCacheKey('najaf'), 'najaf')
})

test('cache returns stale flag after ttl', () => {
  const cache = createCityListCache(1)
  cache.set('najaf', [{ id: 1 }], [])
  const fresh = cache.get('najaf')
  assert.equal(fresh.stale, false)
  assert.equal(fresh.rows[0].id, 1)
})

test('capEvaluationRows limits batch size', () => {
  const rows = Array.from({ length: 100 }, (_, i) => ({ id: i }))
  assert.equal(capEvaluationRows(rows, 40).length, 40)
  assert.equal(capEvaluationRows(rows.slice(0, 10), 40).length, 10)
})
