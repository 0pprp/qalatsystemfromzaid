import assert from 'node:assert/strict'
import { aggregateBranches, categorizeItem, collectedShare, growthPercent, isAqeelBranch, isExcludedOverviewBranch, normalizeBranch } from './salesOverview.js'

assert.equal(collectedShare(0, 0), 0)
assert.equal(collectedShare(250, 1000), 25)
assert.equal(collectedShare(1500, 1000), 100)
assert.equal(growthPercent(0, 0), 0)
assert.equal(growthPercent(10, 0), null)
assert.equal(growthPercent(125, 100), 25)
assert.equal(growthPercent(50, 100), -50)

assert.equal(categorizeItem('ايفون 13'), 'هواتف')
assert.equal(categorizeItem('دراجة كهربائية'), 'دراجات')
assert.equal(categorizeItem('سكوتر'), 'دراجات')
assert.equal(categorizeItem('لابتوب'), 'حواسيب')
assert.equal(categorizeItem('شاشة 55'), 'شاشات')
assert.equal(categorizeItem('مكيف'), 'أخرى')
assert.equal(isAqeelBranch({ cityName: 'الكرخ - عقيل' }), true)
assert.equal(isAqeelBranch({ cityName: 'النجف' }), false)
assert.equal(isAqeelBranch({ database: 'DatabaseCompanyRusafaAqeel' }), true)
assert.equal(isExcludedOverviewBranch({ cityName: 'الكوت' }), true)
assert.equal(isExcludedOverviewBranch({ cityName: 'ميسان', database: 'DatabaseCompanyMaysan' }), true)
assert.equal(isExcludedOverviewBranch({ link: 'http://sharenewkot.alsaaeidy.com/api/' }), true)
assert.equal(isExcludedOverviewBranch({ cityName: 'النجف' }), false)

const merged = aggregateBranches([
  normalizeBranch({
    cityValue: 'najaf',
    cityName: 'النجف',
    monthSalesCount: 4,
    monthSalePrice: 400,
    previousMonthSalesCount: 2,
    previousMonthSalePrice: 200,
    items: [{ itemName: 'ايفون', quantity: 2, salesCount: 2, salePrice: 400 }],
    recommenders: [{ name: 'علي', role: 'متابع', salesCount: 2, salePrice: 300 }],
  }),
  normalizeBranch({
    cityValue: 'basra',
    cityName: 'البصرة',
    monthSalesCount: 6,
    monthSalePrice: 900,
    previousMonthSalesCount: 6,
    previousMonthSalePrice: 600,
    items: [
      { itemName: 'ايفون', quantity: 1, salesCount: 1, salePrice: 100 },
      { itemName: 'دراجة كهربائية', quantity: 3, salesCount: 3, salePrice: 800 },
    ],
    recommenders: [{ name: 'علي', role: 'متابع', salesCount: 1, salePrice: 100 }],
  }),
])

assert.equal(merged.monthSalesCount, 10)
assert.equal(merged.monthSalePrice, 1300)
assert.equal(merged.salesCountGrowthPercent, 25)
assert.equal(merged.salePriceGrowthPercent, 62.5)
assert.equal(merged.items[0].itemName, 'دراجة كهربائية')
assert.equal(merged.items[1].quantity, 3)
assert.equal(merged.items[1].category, 'هواتف')
assert.equal(merged.recommenders.length, 1)
assert.equal(merged.recommenders[0].salesCount, 3)
assert.equal(merged.recommenders[0].salePrice, 400)
assert.equal(normalizeBranch({
  monthSalesCount: 8,
  previousMonthSalesCount: 5,
  monthSalePrice: 150,
  previousMonthSalePrice: 100,
}).salePriceGrowthPercent, 50)

const scored = aggregateBranches([
  normalizeBranch({
    monthSalesCount: 3,
    scoredSalesCount: 2,
    excellentSales: 1,
    badSales: 1,
    scoreSum: 0,
    sellers: [{ name: 'أحمد', salesCount: 3, salePrice: 300, scoredSales: 2, excellentSales: 1, badSales: 1, scoreSum: 0 }],
    saleScores: [{ label: 'ممتاز', score: 10, count: 1, salePrice: 100 }, { label: 'سيء', score: -10, count: 1, salePrice: 80 }],
  }),
  normalizeBranch({
    monthSalesCount: 2,
    scoredSalesCount: 2,
    excellentSales: 2,
    scoreSum: 20,
    sellers: [{ name: 'أحمد', salesCount: 2, salePrice: 200, scoredSales: 2, excellentSales: 2, scoreSum: 20 }],
    saleScores: [{ label: 'ممتاز', score: 10, count: 2, salePrice: 200 }],
  }),
])
assert.equal(scored.averageSaleScore, 5)
assert.equal(scored.unscoredSalesCount, 1)
assert.equal(scored.excellentSales, 3)
assert.equal(scored.badSales, 1)
assert.equal(scored.sellers[0].averageScore, 5)
assert.equal(scored.sellers[0].excellentSales, 3)
assert.equal(scored.saleScores.find(row => row.label === 'ممتاز').count, 3)

const collected = aggregateBranches([
  normalizeBranch({ monthSalePrice: 400, monthCollected: 100 }),
  normalizeBranch({ monthSalePrice: 600, monthCollected: 300 }),
])
assert.equal(collected.monthCollected, 400)
assert.equal(collected.collectedPercent, 40)
assert.equal(normalizeBranch({ monthSalePrice: 200, monthCollected: 50 }).collectedPercent, 25)

console.log('salesOverview tests passed')
