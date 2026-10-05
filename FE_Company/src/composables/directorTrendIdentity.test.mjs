import assert from 'node:assert/strict'
import test from 'node:test'
import { listTrendCacheKey, trendMatchesRequest } from './directorTrendIdentity.js'

test('rejects follower payload when list was requested', () => {
  assert.equal(
    trendMatchesRequest({ scope: 'follower', id: 1, endValue: 0.7 }, 'list', 99),
    false,
  )
})

test('accepts matching list payload', () => {
  assert.equal(
    trendMatchesRequest({ Scope: 'list', Id: 42, EndValue: 2.46 }, 'list', 42),
    true,
  )
})

test('rejects same id under different scope', () => {
  assert.equal(
    trendMatchesRequest({ scope: 'follower', id: 5, endValue: 1 }, 'list', 5),
    false,
  )
})

test('listTrendCacheKey is city|list|id|dates', () => {
  assert.equal(
    listTrendCacheKey({
      cityValue: 'DatabaseCompanyNajaf',
      listId: 12,
      fromDate: '2026-10-01',
      toDate: '2026-10-03',
    }),
    'DatabaseCompanyNajaf|list|12|2026-10-01|2026-10-03',
  )
})
