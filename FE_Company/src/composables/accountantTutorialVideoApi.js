import { salesGatewayBase } from '@/composables/useSalesBranches'
import {
  ACCOUNTANT_TUTORIAL_APP_KEY,
  ACCOUNTANT_TUTORIAL_VIDEO_URL,
} from '@/config/accountantTutorialVideo'

function isHttpUrl(raw) {
  const url = String(raw || '').trim()
  if (!url)
    return ''
  try {
    const parsed = new URL(url)
    if (parsed.protocol !== 'http:' && parsed.protocol !== 'https:')
      return ''

    return url
  }
  catch {
    return ''
  }
}

/** Resolve tutorial video URL: env/config first, then gateway MobileAppTutorialLinks. */
export async function resolveAccountantTutorialVideoUrl() {
  const fromConfig = isHttpUrl(ACCOUNTANT_TUTORIAL_VIDEO_URL)
  if (fromConfig)
    return fromConfig

  try {
    const base = salesGatewayBase()
    const key = encodeURIComponent(ACCOUNTANT_TUTORIAL_APP_KEY)
    const res = await fetch(`${base}mobile-tutorial-links/${key}`, {
      method: 'GET',
      headers: { Accept: 'application/json' },
    })
    if (!res.ok)
      return ''

    const data = await res.json().catch(() => null)
    const raw = data?.videoUrl ?? data?.VideoUrl ?? ''

    return isHttpUrl(raw)
  }
  catch {
    return ''
  }
}
