/**
 * Canonical province matching for GetAdmin catalog entries.
 * Display name ≠ value ≠ database — never confuse them.
 */

export function canonicalCityKey(value) {
  if (value == null || value === '')
    return ''

  return String(value).trim()
}

function provinceMatches(province, key) {
  return canonicalCityKey(province.value) === key
    || canonicalCityKey(province.name) === key
    || canonicalCityKey(province.database) === key
}

/**
 * @param {Array<{ value?: any, name?: string, database?: string, link?: string }>} provinces
 * @param {string|number|null|undefined} cityKey
 * @returns {object|null} exact one match, or null if zero/ambiguous
 */
export function findProvince(provinces, cityKey) {
  const key = canonicalCityKey(cityKey)
  if (!key || !Array.isArray(provinces))
    return null

  const matches = provinces.filter(p => provinceMatches(p, key))
  if (matches.length !== 1)
    return null

  return matches[0]
}

/**
 * Race-safe load helper: only the latest generation may commit results.
 */
export function createRequestGeneration() {
  let gen = 0

  return {
    next() {
      gen += 1

      return gen
    },
    isCurrent(token) {
      return token === gen
    },
  }
}
