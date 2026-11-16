import assert from 'node:assert/strict'
import {
  formatHiddenFinancial,
  isLegalCustomer,
  shouldHidePaidRemaining,
  sumVisibleFinancial,
  visibleAmountRemaining,
  visibleReceiptsTotal,
} from './legalFinancialVisibility.js'

const chief = { userType: 'محاسب رئيسي' }
const other = { userType: 'مدير مبيعات' }

assert.equal(isLegalCustomer({ isLegal: true }), true)
assert.equal(isLegalCustomer({ IsLegal: 'true' }), true)
assert.equal(isLegalCustomer({ isLegal: false }), false)

assert.equal(shouldHidePaidRemaining({ isLegal: true }, other), true)
assert.equal(shouldHidePaidRemaining({ isLegal: true }, chief), false)
assert.equal(shouldHidePaidRemaining({ HidePaidRemaining: true }, other), true)
assert.equal(shouldHidePaidRemaining({ isLegal: false }, other), false)
assert.equal(shouldHidePaidRemaining({ isLegal: false }, { showType: 'القانونية', userType: 'المدير' }), true)
assert.equal(shouldHidePaidRemaining({ isLegal: true }, { showType: 'القانونية', ...chief }), false)

assert.equal(visibleReceiptsTotal({ isLegal: true, receiptsTotal: 9000 }, other), null)
assert.equal(visibleReceiptsTotal({ isLegal: true, receiptsTotal: 9000 }, chief), 9000)
assert.equal(visibleAmountRemaining({ isLegal: true, amountRemaining: 1000 }, other), null)
assert.equal(visibleAmountRemaining({ isLegal: false, amountRemaining: 1000 }, other), 1000)
assert.equal(formatHiddenFinancial(null), '—')

const sum = sumVisibleFinancial(
  [
    { isLegal: true, receiptsTotal: 50 },
    { isLegal: false, receiptsTotal: 30 },
  ],
  r => r.receiptsTotal,
  other,
)
assert.equal(sum, 30)

const sumChief = sumVisibleFinancial(
  [
    { isLegal: true, receiptsTotal: 50 },
    { isLegal: false, receiptsTotal: 30 },
  ],
  r => r.receiptsTotal,
  chief,
)
assert.equal(sumChief, 80)

const sumLegalView = sumVisibleFinancial(
  [{ isLegal: false, receiptsTotal: 99 }],
  r => r.receiptsTotal,
  { showType: 'القانونية', userType: 'المراقب' },
)
assert.equal(sumLegalView, 0)

console.log('legalFinancialVisibility.test.mjs OK')
