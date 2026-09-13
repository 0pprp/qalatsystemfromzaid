import assert from 'node:assert/strict'
import test from 'node:test'
import { excelCellText, excelHeaderKey } from './salesRequestExcelImport.js'

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
