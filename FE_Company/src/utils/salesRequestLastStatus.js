/**
 * Resolve «تاريخ آخر حالة» for sales-request cards (monitor / director / sales manager).
 */

function pick(row, ...keys) {
  if (!row || typeof row !== 'object')
    return undefined
  for (const key of keys) {
    if (row[key] != null && row[key] !== '')
      return row[key]
  }

  return undefined
}

function asMs(value) {
  if (value == null || value === '')
    return Number.NaN
  if (typeof value === 'number')
    return Number.isFinite(value) ? value : Number.NaN
  if (value instanceof Date)
    return value.getTime()

  const text = String(value).trim()
  const hasZone = /Z$|[+-]\d{2}:?\d{2}$/i.test(text)
  const normalized = hasZone ? text : `${text.replace(' ', 'T')}Z`

  return Date.parse(normalized)
}

/**
 * Best-known UTC instant when the current status was entered.
 * Prefers API LastStatusAtUtc (from SalesRequestHistory), then status-specific columns.
 */
export function resolveLastStatusAtUtc(row, status) {
  const fromApi = pick(row, 'lastStatusAtUtc', 'LastStatusAtUtc')
  if (fromApi != null && fromApi !== '')
    return fromApi

  const s = String(status || pick(row, 'status', 'Status') || '').trim()
  const created = pick(row, 'createdAtUtc', 'CreatedAtUtc')
  const assigned = pick(row, 'assignedAtUtc', 'AssignedAtUtc')
  const viewed = pick(row, 'viewedAtUtc', 'ViewedAtUtc')
  const processing = pick(row, 'processingAtUtc', 'ProcessingAtUtc')
  const completed = pick(row, 'completedAtUtc', 'CompletedAtUtc')
  const rejected = pick(row, 'rejectedAtUtc', 'RejectedAtUtc')
  const deleted = pick(row, 'deletedAtUtc', 'DeletedAtUtc')
  const filtered = pick(row, 'filteredAtUtc', 'FilteredAtUtc')

  switch (s) {
    case 'New':
      return created || null
    case 'Assigned':
    case 'Returned':
      return assigned || created || null
    case 'Viewed':
      return viewed || assigned || created || null
    case 'Pending':
      // No PendingAtUtc column — prefer history via LastStatusAtUtc; else latest known stamp.
      return newestOf(viewed, assigned, processing, filtered, created)
    case 'PreparedForSale':
    case 'InProgress':
    case 'ConvertedToSale':
      return processing || viewed || assigned || created || null
    case 'Inspected':
      return processing || completed || viewed || assigned || created || null
    case 'Completed':
      return completed || processing || created || null
    case 'Rejected':
      return rejected || deleted || created || null
    case 'LegalFollowup':
      return newestOf(filtered, rejected, processing, assigned, created)
    default:
      return newestOf(completed, rejected, processing, viewed, assigned, deleted, created)
  }
}

function newestOf(...values) {
  let best = null
  let bestMs = -Infinity
  for (const value of values) {
    const ms = asMs(value)
    if (!Number.isFinite(ms))
      continue
    if (ms >= bestMs) {
      bestMs = ms
      best = value
    }
  }

  return best
}
