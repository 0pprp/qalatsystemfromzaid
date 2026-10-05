/**
 * Stale-while-revalidate cache for sales-request lists keyed by province.
 */

export function cityCacheKey(cityValue) {
  const v = String(cityValue || '').trim()
  return v || '__all__'
}

export function createCityListCache(ttlMs = 60_000) {
  const map = new Map()

  return {
    get(cityValue) {
      const key = cityCacheKey(cityValue)
      const hit = map.get(key)
      if (!hit)
        return null
      return {
        ...hit,
        stale: Date.now() - hit.at > ttlMs,
      }
    },
    set(cityValue, rows, deletedRows) {
      map.set(cityCacheKey(cityValue), {
        rows: Array.isArray(rows) ? rows : [],
        deletedRows: Array.isArray(deletedRows) ? deletedRows : [],
        at: Date.now(),
      })
    },
    invalidate(cityValue) {
      if (cityValue === undefined)
        map.clear()
      else
        map.delete(cityCacheKey(cityValue))
    },
  }
}

/** Cap evaluation batch size so tab=all does not hammer the catalog. */
export function capEvaluationRows(list, max = 40) {
  if (!Array.isArray(list) || list.length <= max)
    return list || []
  return list.slice(0, max)
}
