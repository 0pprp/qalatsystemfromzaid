/**
 * Pure helpers for SalesRequest evaluation summary indexing / lookup.
 * Fixes silent "0 نتائج" when API returned counts under a different key alias.
 */

export function requestCityOf(row) {
  if (!row || typeof row !== 'object')
    return ''

  return String(row.cityValue ?? row.CityValue ?? row.sourceCityValue ?? row.SourceCityValue ?? '').trim()
}

export function requestIdOf(row) {
  if (!row || typeof row !== 'object')
    return 0
  const n = Number(row.id ?? row.Id ?? 0)

  return Number.isFinite(n) ? n : 0
}

export function buildEvaluationKey(cityValue, requestId) {
  const city = String(cityValue ?? '').trim()
  const id = Number(requestId ?? 0)

  return `${city}:${Number.isFinite(id) ? id : 0}`
}

export function evaluationKeyFromRow(row) {
  return buildEvaluationKey(requestCityOf(row), requestIdOf(row))
}

export function evaluationKeysFromItem(item) {
  if (!item || typeof item !== 'object')
    return []
  const requestId = item.requestId ?? item.RequestId
  const source = String(item.sourceCityValue ?? item.SourceCityValue ?? '').trim()
  const keys = [
    item.key,
    item.Key,
    buildEvaluationKey(source, requestId),
  ]

  return [...new Set(keys.filter(k => k != null && String(k).length > 0).map(k => String(k)))]
}

/**
 * Index API evaluation items under every stable alias so FE cards can resolve them.
 */
export function indexEvaluationItems(items, requestRows = []) {
  const map = Object.create(null)
  const rows = Array.isArray(requestRows) ? requestRows : []
  for (const item of items || []) {
    if (!item)
      continue
    const keys = evaluationKeysFromItem(item)
    const rid = Number(item.requestId ?? item.RequestId ?? 0)
    const src = String(item.sourceCityValue ?? item.SourceCityValue ?? '').trim()
    for (const row of rows) {
      if (requestIdOf(row) !== rid)
        continue
      const rowCity = requestCityOf(row)
      if (!src || !rowCity || rowCity.toLowerCase() === src.toLowerCase())
        keys.push(evaluationKeyFromRow(row))
    }
    for (const key of [...new Set(keys)])
      map[key] = item
  }

  return map
}

export function lookupEvaluation(map, row) {
  if (!map || !row)
    return null
  const primary = evaluationKeyFromRow(row)
  if (map[primary])
    return map[primary]
  const id = requestIdOf(row)
  const city = requestCityOf(row)
  const aliases = [
    buildEvaluationKey(city, id),
    `${city}:${id}`,
  ]
  for (const key of aliases) {
    if (map[key])
      return map[key]
  }
  // Case-insensitive city scan for same request id (Admin value vs legacy stamp).
  const cityLower = city.toLowerCase()
  for (const [key, item] of Object.entries(map)) {
    const itemId = Number(item?.requestId ?? item?.RequestId ?? 0)
    if (itemId !== id)
      continue
    const itemCity = String(item?.sourceCityValue ?? item?.SourceCityValue ?? '').trim()
    if (itemCity.toLowerCase() === cityLower)
      return item
    if (String(key).toLowerCase() === primary.toLowerCase())
      return item
  }

  return null
}

export function categoryBlock(evalRow, key) {
  if (!evalRow)
    return null
  const map = {
    tripleName: evalRow.tripleName || evalRow.TripleName,
    phone: evalRow.phone || evalRow.Phone,
    fatherGrandfather: evalRow.fatherGrandfather || evalRow.FatherGrandfather,
  }

  return map[key] || null
}

export function categoryResultCount(cat) {
  if (!cat)
    return null
  const n = cat.resultCount ?? cat.ResultCount
  if (n == null || n === '')
    return null
  const num = Number(n)

  return Number.isFinite(num) ? num : null
}

export function categoryWorstLabel(cat) {
  if (!cat)
    return null

  return cat.worstRatingLabel || cat.WorstRatingLabel || null
}

export function categoryWorstScore(cat) {
  if (!cat)
    return null
  const n = cat.worstScore ?? cat.WorstScore
  if (n == null || n === '')
    return null
  const num = Number(n)

  return Number.isFinite(num) ? num : null
}

export function settlementDaysDisplay(hit) {
  if (!hit || typeof hit !== 'object')
    return null
  const settle = hit.daysToSettle ?? hit.DaysToSettle
  if (settle != null && settle !== '' && Number(settle) > 0)
    return Number(settle)
  const since = hit.daysSinceSale ?? hit.DaysSinceSale
  if (since != null && since !== '' && Number(since) > 0)
    return Number(since)

  return null
}

export function receiptCountDisplay(hit) {
  if (!hit || typeof hit !== 'object')
    return null
  const n = hit.receiptCount ?? hit.ReceiptCount
  if (n == null || n === '')
    return null
  const num = Number(n)

  return Number.isFinite(num) ? num : null
}
