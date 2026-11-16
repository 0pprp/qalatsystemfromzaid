export function collectedShare(collected, salePrice) {
  const paid = Number(collected) || 0
  const price = Number(salePrice) || 0
  if (price <= 0 || paid <= 0)
    return 0
  const capped = Math.min(paid, price)

  return Math.round((capped / price) * 1000) / 10
}

export function growthPercent(current, previous) {
  const c = Number(current) || 0
  const p = Number(previous) || 0
  if (p === 0)
    return c === 0 ? 0 : null

  return Math.round(((c - p) / p) * 1000) / 10
}

export function categorizeItem(name) {
  const text = String(name || '').trim()
  if (!text)
    return 'أخرى'

  const has = (...parts) => parts.some(part => text.toLowerCase().includes(String(part).toLowerCase()))
  if (has('دراجة', 'بايك', 'بايسكل', 'سكوتر', 'bike', 'ebike'))
    return 'دراجات'
  if (has('هاتف', 'جوال', 'موبايل', 'ايفون', 'آيفون', 'أيفون', 'سامسونج', 'شاومي', 'ريدمي', 'phone', 'iphone'))
    return 'هواتف'
  if (has('لابتوب', 'حاسوب', 'كمبيوتر', 'laptop'))
    return 'حواسيب'
  if (has('شاشة', 'تلفزيون', 'تلفاز'))
    return 'شاشات'

  return 'أخرى'
}

function branchIdentity(branch) {
  return {
    name: String(branch?.cityName || branch?.CityName || branch?.name || branch?.Name || ''),
    database: String(branch?.database || branch?.Database || ''),
    link: String(branch?.link || branch?.Link || ''),
  }
}

export function isAqeelBranch(branch) {
  const { name, database, link } = branchIdentity(branch)

  return name.includes('عقيل') || /aqeel/i.test(database) || /aqeel/i.test(link)
}

export function isExcludedOverviewBranch(branch) {
  const { name, database, link } = branchIdentity(branch)

  return isAqeelBranch(branch)
    || name.includes('كوت')
    || name.includes('ميسان')
    || /kot|maysan/i.test(database)
    || /kot|maysan/i.test(link)
}

export function roleLabel(role) {
  const text = String(role || '').trim()
  if (!text)
    return ''
  if (text === 'Follower' || text === 'متابع')
    return 'متابع'
  if (text === 'Delegate' || text === 'مندوب')
    return 'مندوب'
  if (text === 'Crm' || text === 'قسم التسويق')
    return 'قسم التسويق'
  if (text === 'موظف مبيعات' || text === 'SalesEmployee')
    return 'موظف مبيعات'
  if (text === 'مدير مبيعات' || text === 'SalesManager')
    return 'مدير مبيعات'

  return text
}

function pick(raw, ...keys) {
  for (const key of keys) {
    if (raw && raw[key] != null && raw[key] !== '')
      return raw[key]
  }

  return undefined
}

function num(raw, ...keys) {
  const value = Number(pick(raw, ...keys) ?? 0)

  return Number.isFinite(value) ? value : 0
}

function growth(raw, ...keys) {
  const value = pick(raw, ...keys)
  if (value == null || value === '')
    return null
  const parsed = Number(value)

  return Number.isFinite(parsed) ? parsed : null
}

function asList(raw, ...keys) {
  const value = pick(raw, ...keys)

  return Array.isArray(value) ? value : []
}

export function normalizeBranch(raw, fallback = {}) {
  const items = asList(raw, 'items', 'Items').map(row => ({
    itemName: String(pick(row, 'itemName', 'ItemName') || 'بدون اسم'),
    category: String(pick(row, 'category', 'Category') || categorizeItem(pick(row, 'itemName', 'ItemName'))),
    quantity: num(row, 'quantity', 'Quantity'),
    salesCount: num(row, 'salesCount', 'SalesCount'),
    salePrice: num(row, 'salePrice', 'SalePrice'),
  }))

  return {
    cityValue: String(pick(raw, 'cityValue', 'CityValue') ?? fallback.cityValue ?? ''),
    cityName: String(pick(raw, 'cityName', 'CityName') ?? fallback.cityName ?? 'الفرع'),
    year: num(raw, 'year', 'Year') || fallback.year || 0,
    month: num(raw, 'month', 'Month') || fallback.month || 0,
    daysInMonth: num(raw, 'daysInMonth', 'DaysInMonth'),
    employeesOnShift: num(raw, 'employeesOnShift', 'EmployeesOnShift'),
    employeesOffShift: num(raw, 'employeesOffShift', 'EmployeesOffShift'),
    liveLocations: num(raw, 'liveLocations', 'LiveLocations'),
    salesToday: num(raw, 'salesToday', 'SalesToday'),
    pendingSales: num(raw, 'pendingSales', 'PendingSales'),
    newSalesRequests: num(raw, 'newSalesRequests', 'NewSalesRequests'),
    monthSalesCount: num(raw, 'monthSalesCount', 'MonthSalesCount'),
    monthSalePrice: num(raw, 'monthSalePrice', 'MonthSalePrice'),
    monthCollected: num(raw, 'monthCollected', 'MonthCollected'),
    collectedPercent: collectedShare(
      num(raw, 'monthCollected', 'MonthCollected'),
      num(raw, 'monthSalePrice', 'MonthSalePrice'),
    ),
    previousMonthSalesCount: num(raw, 'previousMonthSalesCount', 'PreviousMonthSalesCount'),
    previousMonthSalePrice: num(raw, 'previousMonthSalePrice', 'PreviousMonthSalePrice'),
    salesCountGrowthPercent: growth(raw, 'salesCountGrowthPercent', 'SalesCountGrowthPercent')
      ?? growthPercent(num(raw, 'monthSalesCount', 'MonthSalesCount'), num(raw, 'previousMonthSalesCount', 'PreviousMonthSalesCount')),
    salePriceGrowthPercent: growth(raw, 'salePriceGrowthPercent', 'SalePriceGrowthPercent')
      ?? growthPercent(num(raw, 'monthSalePrice', 'MonthSalePrice'), num(raw, 'previousMonthSalePrice', 'PreviousMonthSalePrice')),
    averageSalePrice: num(raw, 'averageSalePrice', 'AverageSalePrice'),
    monthDownPayment: num(raw, 'monthDownPayment', 'MonthDownPayment'),
    monthItemQuantity: num(raw, 'monthItemQuantity', 'MonthItemQuantity'),
    ordersCreated: num(raw, 'ordersCreated', 'OrdersCreated'),
    ordersConverted: num(raw, 'ordersConverted', 'OrdersConverted'),
    ordersFinished: num(raw, 'ordersFinished', 'OrdersFinished'),
    ordersRejected: num(raw, 'ordersRejected', 'OrdersRejected'),
    ordersOpen: num(raw, 'ordersOpen', 'OrdersOpen'),
    ordersPending: num(raw, 'ordersPending', 'OrdersPending'),
    ordersUnassigned: num(raw, 'ordersUnassigned', 'OrdersUnassigned'),
    orderConversionPercent: num(raw, 'orderConversionPercent', 'OrderConversionPercent'),
    scoredSalesCount: num(raw, 'scoredSalesCount', 'ScoredSalesCount'),
    unscoredSalesCount: num(raw, 'unscoredSalesCount', 'UnscoredSalesCount'),
    excellentSales: num(raw, 'excellentSales', 'ExcellentSales'),
    goodSales: num(raw, 'goodSales', 'GoodSales'),
    weakSales: num(raw, 'weakSales', 'WeakSales'),
    laggingSales: num(raw, 'laggingSales', 'LaggingSales'),
    badSales: num(raw, 'badSales', 'BadSales'),
    scoreSum: num(raw, 'scoreSum', 'ScoreSum'),
    averageSaleScore: growth(raw, 'averageSaleScore', 'AverageSaleScore'),
    daily: asList(raw, 'daily', 'Daily').map(row => ({
      day: num(row, 'day', 'Day'),
      salesCount: num(row, 'salesCount', 'SalesCount'),
      salePrice: num(row, 'salePrice', 'SalePrice'),
    })),
    items,
    categories: asList(raw, 'categories', 'Categories').map(row => ({
      category: String(pick(row, 'category', 'Category') || 'أخرى'),
      quantity: num(row, 'quantity', 'Quantity'),
      salesCount: num(row, 'salesCount', 'SalesCount'),
      salePrice: num(row, 'salePrice', 'SalePrice'),
    })),
    recommenders: asList(raw, 'recommenders', 'Recommenders').map(row => ({
      name: String(pick(row, 'name', 'Name') || 'بدون موصي'),
      role: String(pick(row, 'role', 'Role') || ''),
      salesCount: num(row, 'salesCount', 'SalesCount'),
      salePrice: num(row, 'salePrice', 'SalePrice'),
    })),
    sellers: asList(raw, 'sellers', 'Sellers').map(row => {
      const scoredSales = num(row, 'scoredSales', 'ScoredSales')
      const scoreSum = num(row, 'scoreSum', 'ScoreSum')

      return {
        name: String(pick(row, 'name', 'Name') || 'بدون اسم'),
        salesCount: num(row, 'salesCount', 'SalesCount'),
        salePrice: num(row, 'salePrice', 'SalePrice'),
        scoredSales,
        excellentSales: num(row, 'excellentSales', 'ExcellentSales'),
        goodSales: num(row, 'goodSales', 'GoodSales'),
        weakSales: num(row, 'weakSales', 'WeakSales'),
        laggingSales: num(row, 'laggingSales', 'LaggingSales'),
        badSales: num(row, 'badSales', 'BadSales'),
        scoreSum,
        averageScore: growth(row, 'averageScore', 'AverageScore')
          ?? (scoredSales ? Math.round((scoreSum / scoredSales) * 10) / 10 : null),
      }
    }),
    saleScores: asList(raw, 'saleScores', 'SaleScores').map(row => ({
      label: String(pick(row, 'label', 'Label') || ''),
      score: num(row, 'score', 'Score'),
      count: num(row, 'count', 'Count'),
      salePrice: num(row, 'salePrice', 'SalePrice'),
    })),
    orderStatuses: asList(raw, 'orderStatuses', 'OrderStatuses').map(row => ({
      status: String(pick(row, 'status', 'Status') || ''),
      count: num(row, 'count', 'Count'),
    })),
    overviewError: String(pick(raw, 'overviewError', 'OverviewError') || ''),
  }
}

function addSeller(map, row) {
  const current = map.get(row.name) || {
    name: row.name,
    salesCount: 0,
    salePrice: 0,
    scoredSales: 0,
    excellentSales: 0,
    goodSales: 0,
    weakSales: 0,
    laggingSales: 0,
    badSales: 0,
    scoreSum: 0,
  }

  current.salesCount += row.salesCount || 0
  current.salePrice += row.salePrice || 0
  current.scoredSales += row.scoredSales || 0
  current.excellentSales += row.excellentSales || 0
  current.goodSales += row.goodSales || 0
  current.weakSales += row.weakSales || 0
  current.laggingSales += row.laggingSales || 0
  current.badSales += row.badSales || 0
  current.scoreSum += row.scoreSum || 0
  current.averageScore = current.scoredSales
    ? Math.round((current.scoreSum / current.scoredSales) * 10) / 10
    : null
  map.set(row.name, current)
}

function addNamed(map, key, row, nameKey) {
  const current = map.get(key) || {
    [nameKey]: row[nameKey],
    role: row.role || '',
    salesCount: 0,
    salePrice: 0,
    quantity: 0,
    category: row.category || '',
    status: row.status || '',
    count: 0,
  }

  current.salesCount += row.salesCount || 0
  current.salePrice += row.salePrice || 0
  current.quantity += row.quantity || 0
  current.count += row.count || 0
  map.set(key, current)
}

export function aggregateBranches(branches) {
  const list = Array.isArray(branches) ? branches : []
  const daily = new Map()
  const items = new Map()
  const recommenders = new Map()
  const sellers = new Map()
  const scores = new Map()
  const statuses = new Map()

  const totals = {
    employeesOnShift: 0,
    employeesOffShift: 0,
    liveLocations: 0,
    salesToday: 0,
    pendingSales: 0,
    newSalesRequests: 0,
    monthSalesCount: 0,
    monthSalePrice: 0,
    monthCollected: 0,
    previousMonthSalesCount: 0,
    previousMonthSalePrice: 0,
    monthDownPayment: 0,
    monthItemQuantity: 0,
    ordersCreated: 0,
    ordersConverted: 0,
    ordersFinished: 0,
    ordersRejected: 0,
    ordersOpen: 0,
    ordersPending: 0,
    ordersUnassigned: 0,
    scoredSalesCount: 0,
    excellentSales: 0,
    goodSales: 0,
    weakSales: 0,
    laggingSales: 0,
    badSales: 0,
    scoreSum: 0,
    year: list[0]?.year || 0,
    month: list[0]?.month || 0,
    daysInMonth: 0,
  }

  for (const branch of list) {
    for (const key of Object.keys(totals)) {
      if (key === 'year' || key === 'month' || key === 'daysInMonth')
        continue
      totals[key] += Number(branch[key] || 0)
    }
    totals.daysInMonth = Math.max(totals.daysInMonth, Number(branch.daysInMonth || 0))
    for (const row of branch.daily || []) {
      const slot = daily.get(row.day) || { day: row.day, salesCount: 0, salePrice: 0 }

      slot.salesCount += row.salesCount
      slot.salePrice += row.salePrice
      daily.set(row.day, slot)
    }
    for (const row of branch.items || [])
      addNamed(items, row.itemName, row, 'itemName')
    for (const row of branch.recommenders || [])
      addNamed(recommenders, `${row.name}|${row.role}`, row, 'name')
    for (const row of branch.sellers || [])
      addSeller(sellers, row)
    for (const row of branch.saleScores || []) {
      const key = row.label || String(row.score)
      const slot = scores.get(key) || { label: row.label, score: row.score, count: 0, salePrice: 0 }

      slot.count += row.count || 0
      slot.salePrice += row.salePrice || 0
      scores.set(key, slot)
    }
    for (const row of branch.orderStatuses || [])
      addNamed(statuses, row.status, row, 'status')
  }

  const days = totals.daysInMonth || (daily.size ? Math.max(...daily.keys()) : 0)
  const dailyRows = []
  for (let day = 1; day <= days; day++)
    dailyRows.push(daily.get(day) || { day, salesCount: 0, salePrice: 0 })

  const itemRows = [...items.values()]
    .map(row => ({
      ...row,
      category: categorizeItem(row.itemName),
    }))
    .sort((a, b) => b.salePrice - a.salePrice || a.itemName.localeCompare(b.itemName, 'ar'))

  const categoryMap = new Map()
  for (const row of itemRows) {
    const slot = categoryMap.get(row.category) || { category: row.category, quantity: 0, salesCount: 0, salePrice: 0 }

    slot.quantity += row.quantity
    slot.salesCount += row.salesCount
    slot.salePrice += row.salePrice
    categoryMap.set(row.category, slot)
  }

  return {
    ...totals,
    salesCountGrowthPercent: growthPercent(totals.monthSalesCount, totals.previousMonthSalesCount),
    salePriceGrowthPercent: growthPercent(totals.monthSalePrice, totals.previousMonthSalePrice),
    collectedPercent: collectedShare(totals.monthCollected, totals.monthSalePrice),
    averageSalePrice: totals.monthSalesCount ? Math.round(totals.monthSalePrice / totals.monthSalesCount) : 0,
    orderConversionPercent: totals.ordersCreated
      ? Math.round((totals.ordersConverted * 1000) / totals.ordersCreated) / 10
      : 0,
    unscoredSalesCount: Math.max(0, totals.monthSalesCount - totals.scoredSalesCount),
    averageSaleScore: totals.scoredSalesCount
      ? Math.round((totals.scoreSum / totals.scoredSalesCount) * 10) / 10
      : null,
    monthItemQuantity: itemRows.length
      ? itemRows.reduce((sum, row) => sum + row.quantity, 0)
      : totals.monthItemQuantity,
    daily: dailyRows,
    items: itemRows,
    categories: [...categoryMap.values()].sort((a, b) => b.salePrice - a.salePrice),
    recommenders: [...recommenders.values()].sort((a, b) => b.salePrice - a.salePrice || a.name.localeCompare(b.name, 'ar')),
    sellers: [...sellers.values()].sort((a, b) => b.salePrice - a.salePrice || a.name.localeCompare(b.name, 'ar')),
    saleScores: [...scores.values()].sort((a, b) => b.score - a.score),
    orderStatuses: [...statuses.values()].sort((a, b) => b.count - a.count),
  }
}

export function branchesFromPayload(raw) {
  const nested = raw?.branches || raw?.Branches
  if (Array.isArray(nested) && nested.length)
    return nested.map(row => normalizeBranch(row))

  return [normalizeBranch(raw)]
}
