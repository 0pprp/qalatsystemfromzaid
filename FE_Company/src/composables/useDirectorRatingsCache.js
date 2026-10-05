/**
 * Session cache for director ratings — survives profile round-trips and hub↔detail.
 * Keys are versioned so stale shapes are ignored after deploys.
 * Every trend/bucket key starts with city|scope|id so provinces and employees never share slots.
 * Singleton mem: warm-on-login and the ratings page must share one writer (avoids clobber).
 */
const CACHE_KEY = 'director-ratings-cache-v8'
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

/** city|scope|id — used to invalidate only the open entity's bucket lists */
export function trendScopePrefix(cacheKey) {
  const parts = String(cacheKey || '').split('|')
  if (parts.length < 3)
    return String(cacheKey || '')

  return `${parts[0]}|${parts[1]}|${parts[2]}`
}

function dropBucketsForPrefix(buckets, prefix) {
  if (!prefix || !buckets || typeof buckets !== 'object')
    return {}
  const next = {}
  for (const [key, value] of Object.entries(buckets)) {
    if (!String(key).startsWith(`${prefix}|`) && String(key) !== prefix)
      next[key] = value
  }

  return next
}

function createCacheApi(initial) {
  let mem = initial && typeof initial === 'object' ? initial : emptyStore()
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
      // Merge by city — warm must not wipe trends/catalogs via a stale clone.
      if (!map || typeof map !== 'object')
        return
      mem.monthByCity = { ...mem.monthByCity, ...map }
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
      mem.buckets = dropBucketsForPrefix(mem.buckets, trendScopePrefix(key))
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

let sharedCache = null

export function useDirectorRatingsCache() {
  if (!sharedCache)
    sharedCache = createCacheApi(readStore())

  return sharedCache
}

/** Test helper — resets singleton between unit tests. */
export function __resetDirectorRatingsCacheForTests() {
  sharedCache = null
}
