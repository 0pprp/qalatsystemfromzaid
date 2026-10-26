import assert from 'node:assert/strict'
import {
  formatHiddenFinancial,
  isLegalCustomer,
  shouldHidePaidRemaining,
  sumVisibleFinancial,
  visibleAmountRemaining,
  visibleReceiptsTotal,
} from './legalFinancialVisibility.js'

assert.equal(isLegalCustomer({ isLegal: true }), true)
assert.equal(isLegalCustomer({ IsLegal: 'true' }), true)
assert.equal(isLegalCustomer({ isLegal: false }), false)

// Hide disabled — legal customers keep الواصل/الباقي visible
assert.equal(shouldHidePaidRemaining({ HidePaidRemaining: true }), false)
assert.equal(shouldHidePaidRemaining({ isLegal: true }), false)
assert.equal(shouldHidePaidRemaining({ isLegal: false }, { showType: 'القانونية' }), false)
assert.equal(visibleReceiptsTotal({ isLegal: true, receiptsTotal: 9000 }), 9000)
assert.equal(visibleAmountRemaining({ isLegal: true, amountRemaining: 1000 }), 1000)
assert.equal(visibleAmountRemaining({ isLegal: false, amountRemaining: 1000 }), 1000)
assert.equal(formatHiddenFinancial(null), '—')

const sum = sumVisibleFinancial(
  [
    { isLegal: true, receiptsTotal: 50 },
    { isLegal: false, receiptsTotal: 30 },
  ],
  r => r.receiptsTotal,
)
assert.equal(sum, 80)

const sumLegalView = sumVisibleFinancial(
  [{ isLegal: false, receiptsTotal: 99 }],
  r => r.receiptsTotal,
  { showType: 'القانونية' },
)
assert.equal(sumLegalView, 99)

console.log('legalFinancialVisibility.test.mjs OK')
