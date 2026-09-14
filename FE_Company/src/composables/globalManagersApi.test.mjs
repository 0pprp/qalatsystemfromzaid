import test from 'node:test'
import assert from 'node:assert/strict'
import { globalManagerUserMessage, isGlobalSalesManagerType } from '../utils/globalManagerMessages.js'
import { USER_TYPE_SALES_MANAGER } from '../utils/userCreationRoles.js'

test('only sales manager type is global', () => {
  assert.equal(isGlobalSalesManagerType(USER_TYPE_SALES_MANAGER), true)
  assert.equal(isGlobalSalesManagerType('محاسب رئيسي'), false)
  assert.equal(isGlobalSalesManagerType('موظف مبيعات'), false)
})

test('success and partial messages', () => {
  assert.equal(
    globalManagerUserMessage({ ok: true, data: { status: 'Success' } }),
    'تم إنشاء مدير المبيعات في جميع الفروع بنجاح',
  )
  assert.equal(
    globalManagerUserMessage({ ok: true, data: { status: 'PartialFailure' } }),
    'تم التنفيذ جزئياً، تعذر تحديث بعض الفروع',
  )
})

test('conflict message lists city names without secrets', () => {
  const msg = globalManagerUserMessage({
    ok: false,
    data: {
      status: 'Conflict',
      failedBranches: [{ cityName: 'الكرخ' }, { cityName: 'البصرة' }],
    },
  })
  assert.match(msg, /الكرخ/)
  assert.match(msg, /البصرة/)
  assert.equal(msg.toLowerCase().includes('password'), false)
})
