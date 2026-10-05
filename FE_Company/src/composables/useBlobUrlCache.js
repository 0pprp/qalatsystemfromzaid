/**
 * In-memory blob URL cache for director/SM KYC + shop images.
 * Survives profile remounts within the same tab session.
 */
const urls = new Map()
const inflight = new Map()

export function cachedBlobUrl(key) {
  return urls.get(String(key || '')) || null
}

export function rememberBlobUrl(key, url) {
  const k = String(key || '')
  if (!k || !url)
    return
  const prev = urls.get(k)
  if (prev && prev !== url)
    URL.revokeObjectURL(prev)
  urls.set(k, url)
}

export function forgetBlobUrl(key) {
  const k = String(key || '')
  const prev = urls.get(k)
  if (prev) {
    URL.revokeObjectURL(prev)
    urls.delete(k)
  }
  inflight.delete(k)
}

export async function loadCachedBlob(key, fetcher) {
  const k = String(key || '')
  if (!k)
    return null
  const hit = urls.get(k)
  if (hit)
    return hit
  if (inflight.has(k))
    return inflight.get(k)

  const task = (async () => {
    try {
      const blob = await fetcher()
      if (!blob || !blob.size || String(blob.type || '').includes('json'))
        return null
      const buffer = await blob.arrayBuffer()
      const bytes = new Uint8Array(buffer.slice(0, 12))
      const isJpeg = bytes[0] === 0xFF && bytes[1] === 0xD8
      const isPng = bytes[0] === 0x89 && bytes[1] === 0x50 && bytes[2] === 0x4E && bytes[3] === 0x47
      const isGif = bytes[0] === 0x47 && bytes[1] === 0x49 && bytes[2] === 0x46
      const isWebp = bytes[0] === 0x52 && bytes[1] === 0x49 && bytes[2] === 0x46 && bytes[3] === 0x46
      const typeHint = String(blob.type || '')
      const looksImage = isJpeg || isPng || isGif || isWebp || typeHint.startsWith('image/')
      if (!looksImage)
        return null
      const type = isPng
        ? 'image/png'
        : isGif
          ? 'image/gif'
          : isWebp
            ? 'image/webp'
            : (typeHint.startsWith('image/') ? typeHint : 'image/jpeg')
      const url = URL.createObjectURL(new Blob([buffer], { type }))
      rememberBlobUrl(k, url)

      return url
    }
    finally {
      inflight.delete(k)
    }
  })()

  inflight.set(k, task)

  return task
}
