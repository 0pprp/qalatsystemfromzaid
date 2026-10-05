import assert from 'node:assert/strict'
import test from 'node:test'
import {
  filterRequestsByQuery,
  findDuplicateRequests,
  isFatherGrandfatherMatch,
  isPhoneMatch,
  isTripleNameMatch,
  normalizeName,
  phoneForMatch,
} from './salesRequestDuplicateSearch.js'

test('normalizes Arabic name letters for matching', () => {
  assert.equal(normalizeName('أحمد إبراهيم علي'), 'احمد ابراهيم علي')
})

test('matches triple name ignoring trailing numeric tokens', () => {
  assert.equal(isTripleNameMatch('علي حسن محمد', 'علي حسن محمد 2'), true)
  assert.equal(isTripleNameMatch('علي حسن محمد', 'علي حسن كريم'), false)
})

test('matches father+grandfather without first name', () => {
  assert.equal(isFatherGrandfatherMatch('سارة حسن محمد', 'أحمد حسن محمد'), true)
  assert.equal(isFatherGrandfatherMatch('سارة حسن محمد', 'أحمد حسن علي'), false)
})

test('matches Iraqi phones across formats', () => {
  assert.equal(phoneForMatch('07800337759'), '7800337759')
  assert.equal(isPhoneMatch('07800337759', '7800337759'), true)
  assert.equal(isPhoneMatch('07800337759', '07999999999'), false)
})

test('filters requests by name or phone regardless of status', () => {
  const rows = [
    { id: 1, customerName: 'علي حسن محمد', customerPhone: '07800337759', status: 'Rejected' },
    { id: 2, customerName: 'زينب سعد كريم', customerPhone: '07701112233', status: 'New' },
    { id: 3, CustomerName: 'علي حسن كريم', CustomerPhone: '07811112222', Status: 'Completed' },
  ]
  assert.deepEqual(filterRequestsByQuery(rows, 'علي حسن').map(r => r.id), [1, 3])
  assert.deepEqual(filterRequestsByQuery(rows, '07800337759').map(r => r.id), [1])
  assert.deepEqual(filterRequestsByQuery(rows, '   '), rows)
})

test('finds duplicates by category excluding self', () => {
  const rows = [
    { id: 10, cityValue: 'najaf', customerName: 'علي حسن محمد', customerPhone: '07800000001', status: 'New' },
    { id: 11, cityValue: 'basra', customerName: 'علي حسن محمد', customerPhone: '07800000002', status: 'Rejected' },
    { id: 12, cityValue: 'najaf', customerName: 'سارة حسن محمد', customerPhone: '07800000001', status: 'Completed' },
    { id: 13, cityValue: 'karbala', customerName: 'زينب سعد كريم', customerPhone: '07701112233', status: 'New' },
  ]
  const self = rows[0]
  assert.deepEqual(findDuplicateRequests(self, rows, 'tripleName').map(r => r.id), [11])
  assert.deepEqual(findDuplicateRequests(self, rows, 'phone').map(r => r.id), [12])
  assert.deepEqual(findDuplicateRequests(self, rows, 'fatherGrandfather').map(r => r.id), [11, 12])
})
