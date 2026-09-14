import test from 'node:test'
import assert from 'node:assert/strict'
import { canonicalCityKey, createRequestGeneration, findProvince } from './provinceCatalog.js'

const catalog = [
  { value: '1', name: 'النجف', database: 'DatabaseCompanyNajaf', link: 'http://sharenewnajaf.alsaaeidy.com/api/' },
  { value: '3', name: 'الكرخ', database: 'DatabaseCompanyBaghdadKarak', link: 'http://sharenewrkarak.alsaaeidy.com/api/' },
  { value: '9', name: 'البصرة', database: 'DatabaseCompanyBasra', link: 'http://sharenewrbasra.alsaaeidy.com/api/' },
]

test('numeric and string city keys resolve the same province', () => {
  assert.equal(findProvince(catalog, 3)?.database, 'DatabaseCompanyBaghdadKarak')
  assert.equal(findProvince(catalog, '3')?.link, 'http://sharenewrkarak.alsaaeidy.com/api/')
  assert.equal(findProvince(catalog, 1)?.database, 'DatabaseCompanyNajaf')
  assert.equal(findProvince(catalog, '9')?.database, 'DatabaseCompanyBasra')
})

test('login must use selected province link — never silent Najaf', () => {
  const karkh = findProvince(catalog, '3')
  const basra = findProvince(catalog, 9)
  assert.ok(karkh)
  assert.ok(basra)
  assert.equal(karkh.link.includes('karak'), true)
  assert.equal(basra.link.includes('basra'), true)
  assert.equal(karkh.link.includes('najaf'), false)
  assert.equal(basra.link.includes('najaf'), false)
})

test('stale request generation cannot overwrite newer city load', () => {
  const gen = createRequestGeneration()
  const first = gen.next()
  const second = gen.next()
  assert.equal(gen.isCurrent(first), false)
  assert.equal(gen.isCurrent(second), true)
})

test('ambiguous province catalog match is rejected', () => {
  const dupes = [
    ...catalog,
    { value: '3b', name: 'الكرخ', database: 'DatabaseCompanyBaghdadKarakDup', link: 'http://evil.example/api/' },
  ]
  assert.equal(findProvince(dupes, 'الكرخ'), null)
})

test('canonicalCityKey normalizes null/number', () => {
  assert.equal(canonicalCityKey(null), '')
  assert.equal(canonicalCityKey(3), '3')
  assert.equal(canonicalCityKey(' 9 '), '9')
})
