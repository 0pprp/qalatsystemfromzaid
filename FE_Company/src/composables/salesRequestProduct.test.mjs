import assert from 'node:assert/strict'
import test from 'node:test'
import { notesWithoutProduct, requestProduct } from './salesRequestProduct.js'

test('Excel «نوع المبيع» is read as the product', () => {
  assert.equal(requestProduct('نوع المبيع: ثلاجة'), 'ثلاجة')
})

test('CRM, agents-app and hand-typed product lines are read', () => {
  assert.equal(requestProduct('منتج CRM: مبردة'), 'مبردة')
  assert.equal(requestProduct('ملاحظة\nمنتج تطبيق المروّجين: غسالة'), 'غسالة')
  assert.equal(requestProduct('المنتج: تلفزيون'), 'تلفزيون')
})

test('no product line → empty', () => {
  assert.equal(requestProduct(''), '')
  assert.equal(requestProduct(null), '')
  assert.equal(requestProduct('يريد التقسيط'), '')
})

test('notesWithoutProduct drops only the product line', () => {
  assert.equal(notesWithoutProduct('يريد التقسيط\nنوع المبيع: ثلاجة'), 'يريد التقسيط')
  assert.equal(notesWithoutProduct('نوع المبيع: ثلاجة'), '')
})
