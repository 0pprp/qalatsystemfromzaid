import assert from 'node:assert/strict'
import test from 'node:test'
import { excelCellText, excelHeaderKey, parseSalesRequestExcel } from './salesRequestExcelImport.js'

test('phone headers include رقم الهاتف', () => {
  assert.equal(excelHeaderKey('رقم الهاتف'), 'phone')
  assert.equal(excelHeaderKey('الهاتف'), 'phone')
  assert.equal(excelHeaderKey('Phone Number'), 'phone')
  assert.equal(excelHeaderKey('CustomerPhone'), 'phone')
})

test('excelCellText keeps numeric phone digits without inventing leading zero', () => {
  assert.equal(excelCellText(7804924373), '7804924373')
  assert.equal(excelCellText('07804924373'), '07804924373')
  assert.equal(excelCellText('7804924373'), '7804924373')
})

/** Minimal XLSX stub: sheet_to_json returns the matrix we pass via fake workbook. */
function fakeXlsx(matrix) {
  return {
    read() {
      return { SheetNames: ['s'], Sheets: { s: matrix } }
    },
    utils: {
      sheet_to_json(sheet) {
        return sheet
      },
    },
  }
}

const CATALOG = {
  النجف: { cityValue: 'najaf-demo', cityName: 'النجف' },
  البصرة: { cityValue: 'basra-demo', cityName: 'البصرة' },
  الكرخ: { cityValue: 'karkh-demo', cityName: 'الكرخ' },
  الرصافة: { cityValue: 'rusafa-demo', cityName: 'الرصافة' },
  كربلاء: { cityValue: 'karbala-demo', cityName: 'كربلاء' },
}

function resolveCity(provinceText) {
  const key = String(provinceText || '').trim()
  return CATALOG[key] || null
}

test('parseSalesRequestExcel mixed provinces keep distinct cityValue/cityName', () => {
  const matrix = [
    ['اسم الزبون', 'الهاتف', 'المحافظة', 'العنوان', 'نوع المبيع'],
    ['أ', '07801111111', 'النجف', 'كوفة', ''],
    ['ب', '07802222222', 'البصرة', 'معقل', ''],
    ['ج', '07803333333', 'الكرخ', 'منصور', ''],
    ['د', '07804444444', 'الرصافة', 'كرادة', ''],
    ['ه', '07805555555', 'كربلاء', 'حر', ''],
  ]
  const result = parseSalesRequestExcel(matrix, { XLSX: fakeXlsx(matrix), resolveCity })
  assert.equal(result.errors.length, 0)
  assert.equal(result.valid.length, 5)
  assert.deepEqual(
    result.valid.map(r => [r.cityValue, r.cityName]),
    [
      ['najaf-demo', 'النجف'],
      ['basra-demo', 'البصرة'],
      ['karkh-demo', 'الكرخ'],
      ['rusafa-demo', 'الرصافة'],
      ['karbala-demo', 'كربلاء'],
    ],
  )
  assert.ok(result.valid.every(r => r.cityValue !== 'najaf-demo' || r.province === 'النجف'))
})

test('parseSalesRequestExcel rejects blank province', () => {
  const matrix = [
    ['اسم الزبون', 'الهاتف', 'المحافظة'],
    ['أ', '07801111111', ''],
  ]
  const result = parseSalesRequestExcel(matrix, { XLSX: fakeXlsx(matrix), resolveCity })
  assert.equal(result.valid.length, 0)
  assert.equal(result.errors.length, 1)
  assert.match(result.errors[0].message, /المحافظة مطلوبة/)
})

test('parseSalesRequestExcel rejects unknown province without Najaf fallback', () => {
  const matrix = [
    ['اسم الزبون', 'الهاتف', 'المحافظة'],
    ['أ', '07801111111', 'محافظة وهمية'],
  ]
  const result = parseSalesRequestExcel(matrix, {
    XLSX: fakeXlsx(matrix),
    resolveCity: () => null,
  })
  assert.equal(result.valid.length, 0)
  assert.equal(result.errors.length, 1)
  assert.match(result.errors[0].message, /غير معروفة/)
  assert.doesNotMatch(result.errors[0].message, /فلتر/)
})
