/**
 * Prefetch director hub data into session cache right after login.
 */
import {
  directorCities,
  directorStatsBranchMonth,
} from '@/composables/directorApi'
import { useDirectorRatingsCache } from '@/composables/useDirectorRatingsCache'

let warming = null

export function warmDirectorCacheInBackground() {
  if (warming)
    return warming

  warming = (async () => {
    const cache = useDirectorRatingsCache()
    try {
      const cities = await directorCities()
      if (Array.isArray(cities) && cities.length)
        cache.setCities(cities)
    }
    catch { /* keep prior */ }

    try {
      const raw = await directorStatsBranchMonth()
      const rows = Array.isArray(raw) ? raw : (raw ? [raw] : [])
      const map = {}
      for (const row of rows) {
        const key = String(row?.cityValue || row?.CityValue || '').trim()
        if (key)
          map[key] = row
      }
      if (Object.keys(map).length)
        cache.setMonthMap(map)
    }
    catch { /* keep prior */ }
  })().finally(() => {
    warming = null
  })

  return warming
}
