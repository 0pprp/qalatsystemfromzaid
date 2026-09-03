import { parseUtcMillis } from '@/composables/salesManagerApi'

export function haversineMeters(lng1, lat1, lng2, lat2) {
  const R = 6371000
  const toRad = deg => (deg * Math.PI) / 180
  const dLat = toRad(lat2 - lat1)
  const dLng = toRad(lng2 - lng1)
  const a = Math.sin(dLat / 2) ** 2
    + Math.cos(toRad(lat1)) * Math.cos(toRad(lat2)) * Math.sin(dLng / 2) ** 2

  return 2 * R * Math.asin(Math.min(1, Math.sqrt(a)))
}

export function formatIraqTime(value) {
  const ms = typeof value === 'number' ? value : parseUtcMillis(value)
  if (!Number.isFinite(ms))
    return ''

  return new Intl.DateTimeFormat('en-GB', {
    timeZone: 'Asia/Baghdad',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  }).format(new Date(ms))
}

const GRANT = 'LOCATION_PERMISSION_GRANTED'

function eventTime(ev) {
  return parseUtcMillis(ev?.occurredAt ?? ev?.OccurredAt)
}

export function normalizePoints(rawPoints) {
  return (rawPoints || [])
    .map(p => ({
      lng: Number(p.longitude ?? p.Longitude),
      lat: Number(p.latitude ?? p.Latitude),
      t: parseUtcMillis(p.capturedAt ?? p.CapturedAt),
      acc: (p.accuracy ?? p.Accuracy) == null ? null : Number(p.accuracy ?? p.Accuracy),
    }))
    .filter(p => Number.isFinite(p.lng) && Number.isFinite(p.lat)
      && Math.abs(p.lat) <= 90 && Math.abs(p.lng) <= 180
      && Number.isFinite(p.t))
    .sort((a, b) => a.t - b.t)
}

export function dropBeforePermission(rawPoints, events) {
  const points = rawPoints || []
  const grant = [...(events || [])]
    .map(ev => ({ type: String(ev.eventType || ev.EventType || ''), t: eventTime(ev) }))
    .filter(ev => ev.type === GRANT && Number.isFinite(ev.t))
    .sort((a, b) => a.t - b.t)[0]?.t
  if (grant == null)
    return points

  return points.filter(p => parseUtcMillis(p.capturedAt ?? p.CapturedAt ?? p.t) >= grant)
}

export function thinByMeters(points, meters = 5) {
  const HEARTBEAT_MS = 45 * 1000
  const out = []
  for (const p of points) {
    const prev = out[out.length - 1]
    if (!prev) {
      out.push(p)
      continue
    }
    const dist = haversineMeters(prev.lng, prev.lat, p.lng, p.lat)
    const dt = p.t - prev.t
    if (dist >= meters || dt >= HEARTBEAT_MS)
      out.push(p)
  }

  return out
}

export function minuteIndex(points) {
  const seen = new Set()
  const out = []
  points.forEach((p, i) => {
    const key = Math.floor(p.t / 60000)
    if (seen.has(key))
      return
    seen.add(key)
    out.push({ ...p, sourceIndex: i, timeLabel: formatIraqTime(p.t) })
  })

  return out
}

export function segmentByTravel(points) {
  if (!points?.length)
    return []

  const MAX_TELEPORT_M = 8000
  const MAX_SPEED_KMH = 180
  const segments = []
  let current = []

  for (const p of points) {
    if (!current.length) {
      current.push(p)
      continue
    }
    const prev = current[current.length - 1]
    const dist = haversineMeters(prev.lng, prev.lat, p.lng, p.lat)
    const dt = Math.max(1, p.t - prev.t)
    const speedKmh = (dist / dt) * 3600
    if (dist > MAX_TELEPORT_M && speedKmh > MAX_SPEED_KMH) {
      if (current.length)
        segments.push(current)
      current = [p]
      continue
    }
    current.push(p)
  }
  if (current.length)
    segments.push(current)

  return segments
}

function chunkOverlap(list, size, overlap = 1) {
  const out = []
  if (list.length <= size)
    return [list]
  for (let i = 0; i < list.length - 1; i += size - overlap)
    out.push(list.slice(i, Math.min(list.length, i + size)))

  return out
}

function strictlyIncreasingUnix(points) {
  let last = 0

  return points.map(p => {
    let s = Math.floor(p.t / 1000)
    if (s <= last)
      s = last + 1
    last = s

    return s
  })
}

function matchingRadius(point) {
  const acc = Number.isFinite(point?.acc) ? point.acc : 50

  return String(Math.min(80, Math.max(40, Math.round(acc))))
}

function lastCoord(line) {
  return line[line.length - 1]
}

function concatLines(left, right) {
  if (!left?.length)
    return right || []
  if (!right?.length)
    return left
  const end = lastCoord(left)
  const start = right[0]
  if (end[0] === start[0] && end[1] === start[1])
    return left.concat(right.slice(1))

  return left.concat(right)
}

async function fillGap(from, to, token) {
  const dist = haversineMeters(from[0], from[1], to[0], to[1])
  if (dist < 15)
    return []
  if (dist > 40000)
    return [from, to]
  try {
    const res = await fetch(
      `https://api.mapbox.com/directions/v5/mapbox/driving/${from[0]},${from[1]};${to[0]},${to[1]}?geometries=geojson&overview=full&steps=false&access_token=${encodeURIComponent(token)}`,
    )
    const data = await res.json()
    const line = data?.routes?.[0]?.geometry?.coordinates
    if (Array.isArray(line) && line.length >= 2)
      return line
  }
  catch {
    // fall through to a straight connector so the path stays complete
  }

  return [from, to]
}

async function stitchIntoOne(lines, token) {
  const usable = (lines || []).filter(line => Array.isArray(line) && line.length >= 2)
  if (!usable.length)
    return []

  const out = []
  let acc = usable[0]
  for (let i = 1; i < usable.length; i++) {
    const next = usable[i]
    const gap = haversineMeters(lastCoord(acc)[0], lastCoord(acc)[1], next[0][0], next[0][1])
    if (gap > 40000) {
      out.push(acc)
      acc = next
    }
    else if (gap < 20) {
      acc = concatLines(acc, next)
    }
    else {
      const bridge = token ? await fillGap(lastCoord(acc), next[0], token) : [lastCoord(acc), next[0]]
      acc = concatLines(acc, concatLines(bridge, next))
    }
  }
  out.push(acc)

  return out
}

async function matchChunk(points, token) {
  const coordinates = points.map(p => `${p.lng},${p.lat}`).join(';')
  const timestamps = strictlyIncreasingUnix(points).join(';')
  const radiuses = points.map(matchingRadius).join(';')
  const body = new URLSearchParams({
    coordinates,
    timestamps,
    radiuses,
    geometries: 'geojson',
    overview: 'full',
    tidy: 'false',
    steps: 'false',
  })

  const res = await fetch(`https://api.mapbox.com/matching/v5/mapbox/driving?access_token=${encodeURIComponent(token)}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body,
  })
  const data = await res.json()
  if (data?.code === 'Ok' && Array.isArray(data.matchings) && data.matchings.length) {
    return data.matchings
      .map(m => m?.geometry?.coordinates)
      .filter(coords => Array.isArray(coords) && coords.length >= 2)
  }

  return null
}

async function directionsChunk(points, token) {
  if (points.length < 2)
    return []
  const coords = points.map(p => `${p.lng},${p.lat}`).join(';')
  const res = await fetch(
    `https://api.mapbox.com/directions/v5/mapbox/driving/${coords}?geometries=geojson&overview=full&steps=false&access_token=${encodeURIComponent(token)}`,
  )
  const data = await res.json()
  const line = data?.routes?.[0]?.geometry?.coordinates
  if (Array.isArray(line) && line.length >= 2)
    return [line]

  return [points.map(p => [p.lng, p.lat])]
}

async function streetLinesForSegment(segment, token) {
  const lines = []
  for (const part of chunkOverlap(segment, 25, 1)) {
    try {
      const viaMatch = part.length >= 2 ? await matchChunk(part, token) : null
      if (viaMatch?.length === 1)
        lines.push(viaMatch[0])
      else if (viaMatch?.length > 1)
        lines.push(...(await stitchIntoOne(viaMatch, token)))
      else
        lines.push(...await directionsChunk(part, token))
    }
    catch {
      lines.push(part.map(p => [p.lng, p.lat]))
    }
  }

  const stitched = await stitchIntoOne(lines, token)
  if (stitched.length)
    return stitched
  if (segment.length >= 2)
    return [segment.map(p => [p.lng, p.lat])]

  return []
}

export async function matchTrackToRoads(segments, token) {
  const lines = []
  for (const segment of segments) {
    if (segment.length < 2)
      continue
    if (!token) {
      lines.push(segment.map(p => [p.lng, p.lat]))
      continue
    }
    lines.push(...await streetLinesForSegment(segment, token))
  }

  if (lines.length <= 1)
    return lines

  return stitchIntoOne(lines, token)
}

export function lineCollection(lines) {
  return {
    type: 'FeatureCollection',
    features: lines
      .filter(coords => Array.isArray(coords) && coords.length >= 2)
      .map(coordinates => ({
        type: 'Feature',
        geometry: { type: 'LineString', coordinates },
      })),
  }
}

export function pointsCollection(points) {
  return {
    type: 'FeatureCollection',
    features: (points || []).map((p, i) => ({
      type: 'Feature',
      properties: { id: i, time: p.t },
      geometry: { type: 'Point', coordinates: [p.lng, p.lat] },
    })),
  }
}
