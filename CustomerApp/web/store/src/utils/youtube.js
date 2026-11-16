/** Extract a YouTube video id from youtu.be / youtube.com/watch / shorts / embed URLs. */
export function youtubeId(url) {
  if (!url) return null
  try {
    const u = new URL(url)
    if (u.hostname.includes('youtu.be')) return u.pathname.slice(1).split('/')[0] || null
    if (u.hostname.includes('youtube.com') || u.hostname.includes('youtube-nocookie.com')) {
      if (u.searchParams.get('v')) return u.searchParams.get('v')
      const m = u.pathname.match(/\/(embed|shorts|live)\/([^/?]+)/)
      if (m) return m[2]
    }
  } catch { /* invalid url */ }
  return null
}
export function youtubeEmbedUrl(url) {
  const id = youtubeId(url)
  return id ? `https://www.youtube-nocookie.com/embed/${id}?rel=0` : null
}
export function youtubeThumb(url) {
  const id = youtubeId(url)
  return id ? `https://i.ytimg.com/vi/${id}/hqdefault.jpg` : null
}
