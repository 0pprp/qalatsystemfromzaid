/**
 * Ensure a trend API payload belongs to the requested scope/id.
 * Prevents follower ratings from painting over a list drill-down (and vice versa).
 */
export function trendMatchesRequest(data, scope, id) {
  if (!data || typeof data !== 'object')
    return false
  const wantScope = String(scope || '').trim().toLowerCase()
  const wantId = Number(id ?? 0)
  const respScope = String(data.scope ?? data.Scope ?? '').trim().toLowerCase()
  const respId = Number(data.id ?? data.Id ?? 0)

  if (wantScope && respScope && respScope !== wantScope)
    return false
  if (Number.isFinite(wantId) && wantId > 0 && Number.isFinite(respId) && respId > 0 && respId !== wantId)
    return false

  return true
}

export function listTrendCacheKey({ cityValue, listId, fromDate, toDate }) {
  return [
    String(cityValue || ''),
    'list',
    String(listId ?? 0),
    String(fromDate || ''),
    String(toDate || ''),
  ].join('|')
}
