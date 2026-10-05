import assert from 'node:assert/strict'
import test from 'node:test'
import {
  __resetDirectorRatingsCacheForTests,
  bucketCacheKey,
  trendCacheKey,
  trendScopePrefix,
  useDirectorRatingsCache,
} from './useDirectorRatingsCache.js'

test('trendCacheKey includes city + scope + entity id', () => {
  const a = trendCacheKey({
    cityValue: 'DatabaseCompanyNajaf',
    scope: 'employee',
    id: 10,
    fromDate: '2026-10-01',
    toDate: '2026-10-03',
  })
  const b = trendCacheKey({
    cityValue: 'DatabaseCompanyKarbala',
    scope: 'employee',
    id: 10,
    fromDate: '2026-10-01',
    toDate: '2026-10-03',
  })
  assert.notEqual(a, b)
  assert.equal(trendScopePrefix(a), 'DatabaseCompanyNajaf|employee|10')
  assert.equal(trendScopePrefix(b), 'DatabaseCompanyKarbala|employee|10')
})

test('bucket keys for different employees do not share prefix', () => {
  const e1 = bucketCacheKey({
    cityValue: 'DatabaseCompanyNajaf',
    scope: 'employee',
    id: 1,
    level: 'excellent',
    fromDate: '2026-10-01',
    toDate: '2026-10-03',
    q: '',
  })
  const e2 = bucketCacheKey({
    cityValue: 'DatabaseCompanyNajaf',
    scope: 'employee',
    id: 2,
    level: 'excellent',
    fromDate: '2026-10-01',
    toDate: '2026-10-03',
    q: '',
  })
  assert.notEqual(trendScopePrefix(e1), trendScopePrefix(e2))
})

test('useDirectorRatingsCache is a singleton — warm cannot wipe page trends', () => {
  __resetDirectorRatingsCacheForTests()
  const page = useDirectorRatingsCache()
  const warm = useDirectorRatingsCache()
  assert.equal(page, warm)

  const key = trendCacheKey({
    cityValue: 'DatabaseCompanyNajaf',
    scope: 'employee',
    id: 7,
    fromDate: '2026-10-01',
    toDate: '2026-10-03',
  })
  page.setTrend(key, { endValue: 0.5 })
  warm.setMonthMap({ DatabaseCompanyKarbala: { net: 1 } })
  assert.equal(page.getTrend(key)?.endValue, 0.5)
  assert.equal(page.getMonth('DatabaseCompanyKarbala')?.net, 1)
  __resetDirectorRatingsCacheForTests()
})
