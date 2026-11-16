// Minimal per-page <title>/<meta> management (client-side). Social crawlers get server-rendered OG tags
// from the backend route GET /p/{id}; this keeps the browser tab/title and in-app share previews correct.
const SITE = 'قلعة الضمان'
const DEFAULT_DESC =
  'متجر قلعة الضمان: أجهزة كهربائية وأثاث ومستلزمات منزلية بالدفع النقدي أو بالأقساط اليومية لأصحاب المحلات التجارية في العراق.'

function setMeta(attr, key, content) {
  if (typeof document === 'undefined') return
  let el = document.head.querySelector(`meta[${attr}="${key}"]`)
  if (!el) {
    el = document.createElement('meta')
    el.setAttribute(attr, key)
    document.head.appendChild(el)
  }
  el.setAttribute('content', content)
}

export function setHead({ title, description, image, url } = {}) {
  const fullTitle = title ? `${title} | ${SITE}` : `${SITE} | تسوّق نقداً أو بالأقساط اليومية`
  document.title = fullTitle
  const desc = (description || DEFAULT_DESC).replace(/\s+/g, ' ').slice(0, 200)
  setMeta('name', 'description', desc)
  setMeta('property', 'og:title', fullTitle)
  setMeta('property', 'og:description', desc)
  setMeta('property', 'og:url', url || window.location.href)
  if (image && !image.startsWith('data:')) setMeta('property', 'og:image', image)
  else setMeta('property', 'og:image', `${window.location.origin}/LogoCompany.png`)
}
