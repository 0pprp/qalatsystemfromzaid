/**
 * Session cache for director ratings — survives profile round-trips and hub↔detail.
 * Keys are versioned so stale shapes are ignored after deploys.
 */
const CACHE_KEY = 'accountant-ratings-cache-v2'
const TTL_MS = 30 * 60 * 1000

function now() {
  return Date.now()
}

function emptyStore() {
  return {
    savedAt: 0,
    cities: [],
    monthByCity: {},
    catalogs: {},
    trends: {},
    buckets: {},
  }
}

function readStore() {
  try {
    const raw = sessionStorage.getItem(CACHE_KEY)
    if (!raw)
      return emptyStore()
    const parsed = JSON.parse(raw)
    if (!parsed || typeof parsed !== 'object')
      return emptyStore()
    if (!parsed.savedAt || now() - Number(parsed.savedAt) > TTL_MS)
      return emptyStore()

    return {
      savedAt: Number(parsed.savedAt) || 0,
      cities: Array.isArray(parsed.cities) ? parsed.cities : [],
      monthByCity: parsed.monthByCity && typeof parsed.monthByCity === 'object' ? parsed.monthByCity : {},
      catalogs: parsed.catalogs && typeof parsed.catalogs === 'object' ? parsed.catalogs : {},
      trends: parsed.trends && typeof parsed.trends === 'object' ? parsed.trends : {},
      buckets: parsed.buckets && typeof parsed.buckets === 'object' ? parsed.buckets : {},
    }
  }
  catch {
    return emptyStore()
  }
}

function writeStore(store) {
  try {
    sessionStorage.setItem(CACHE_KEY, JSON.stringify({
      ...store,
      savedAt: now(),
    }))
  }
  catch { /* quota / private mode */ }
}

export function trendCacheKey({ cityValue, scope, id, fromDate, toDate }) {
  return [
    String(cityValue || ''),
    String(scope || 'branch'),
    String(id ?? 0),
    String(fromDate || ''),
    String(toDate || ''),
  ].join('|')
}

export function bucketCacheKey({ cityValue, scope, id, level, fromDate, toDate, q }) {
  return [
    String(cityValue || ''),
    String(scope || 'branch'),
    String(id ?? 0),
    String(level || ''),
    String(fromDate || ''),
    String(toDate || ''),
    String(q || ''),
  ].join('|')
}

export function useAccountantRatingsCache() {
  let mem = readStore()
  if (!mem.buckets)
    mem.buckets = {}

  function persist() {
    writeStore(mem)
  }

  function touch() {
    mem.savedAt = now()
    persist()
  }

  return {
    getCities() {
      return mem.cities
    },
    setCities(rows) {
      mem.cities = Array.isArray(rows) ? rows : []
      touch()
    },
    getMonthMap() {
      return { ...mem.monthByCity }
    },
    setMonthMap(map) {
      mem.monthByCity = map && typeof map === 'object' ? map : {}
      touch()
    },
    getMonth(cityValue) {
      const key = String(cityValue || '').trim()

      return key ? mem.monthByCity[key] || null : null
    },
    setMonth(cityValue, row) {
      const key = String(cityValue || '').trim()
      if (!key)
        return
      mem.monthByCity[key] = row
      touch()
    },
    getCatalog(cityValue) {
      const key = String(cityValue || '').trim()

      return key ? mem.catalogs[key] || null : null
    },
    setCatalog(cityValue, catalog) {
      const key = String(cityValue || '').trim()
      if (!key)
        return
      mem.catalogs[key] = catalog
      touch()
    },
    getTrend(key) {
      return key ? mem.trends[key] || null : null
    },
    setTrend(key, trend) {
      if (!key)
        return
      mem.trends[key] = trend
      // Drop only this city/scope/entity buckets — keep other provinces intact.
      const prefix = String(key).split('|').slice(0, 3).join('|')
      const next = {}
      for (const [k, v] of Object.entries(mem.buckets || {})) {
        if (!String(k).startsWith(`${prefix}|`) && String(k) !== prefix)
          next[k] = v
      }
      mem.buckets = next
      touch()
    },
    getBucket(key) {
      return key ? mem.buckets[key] || null : null
    },
    setBucket(key, rows) {
      if (!key)
        return
      mem.buckets[key] = Array.isArray(rows) ? rows : []
      touch()
    },
    clearBuckets() {
      mem.buckets = {}
      touch()
    },
    reloadFromSession() {
      mem = readStore()
      if (!mem.buckets)
        mem.buckets = {}

      return mem
    },
  }
}
