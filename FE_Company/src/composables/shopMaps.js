function pick(obj, ...keys) {
  if (!obj)
    return undefined
  for (const key of keys) {
    if (obj[key] != null && obj[key] !== '')
      return obj[key]
  }

  return undefined
}

function toCoord(value) {
  const n = Number(value)
  if (!Number.isFinite(n))
    return null

  return n
}

export function shopCoordinates(shop) {
  const lat = toCoord(pick(shop, 'latitude', 'Latitude'))
  const lng = toCoord(pick(shop, 'longitude', 'Longitude'))
  if (lat == null || lng == null)
    return null
  if (lat < -90 || lat > 90 || lng < -180 || lng > 180)
    return null

  return { lat, lng }
}

export function googleMapsUrl(shop) {
  const coords = shopCoordinates(shop)
  if (!coords)
    return null

  return `https://www.google.com/maps?q=${coords.lat},${coords.lng}`
}

export function shopCoordsCaption(shop) {
  const coords = shopCoordinates(shop)
  if (!coords)
    return ''

  return `${coords.lat}, ${coords.lng}`
}
