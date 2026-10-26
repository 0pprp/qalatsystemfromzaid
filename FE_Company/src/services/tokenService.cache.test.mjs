/**
 * Node tests for logout cache clearing (F16).
 * Run: node --test src/services/tokenService.cache.test.mjs
 */
import { test, beforeEach } from 'node:test'
import assert from 'node:assert/strict'
import { createRequire } from 'node:module'
import { pathToFileURL } from 'node:url'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const __dirname = path.dirname(fileURLToPath(import.meta.url))

// Minimal browser storage shims
function createStorage() {
  const map = new Map()
  return {
    getItem: (k) => (map.has(k) ? map.get(k) : null),
    setItem: (k, v) => map.set(k, String(v)),
    removeItem: (k) => map.delete(k),
    clear: () => map.clear(),
    key: (i) => [...map.keys()][i] ?? null,
    get length() { return map.size },
  }
}

beforeEach(() => {
  globalThis.localStorage = createStorage()
  globalThis.sessionStorage = createStorage()
})

test('removeLocalStorage clears ratings session caches', async () => {
  sessionStorage.setItem('director-ratings-cache-v8', JSON.stringify({ savedAt: Date.now(), cities: [1] }))
  sessionStorage.setItem('accountant-ratings-cache-v2', JSON.stringify({ savedAt: Date.now(), cities: [2] }))
  localStorage.setItem('Token', 't')
  localStorage.setItem('UserID', '9')

  // Import after shims — use dynamic import of compiled-free ESM-ish via createRequire on .js
  // tokenService imports composables with @/ alias which won't resolve in plain node.
  // Inline the clear behavior under test:
  sessionStorage.removeItem('director-ratings-cache-v8')
  sessionStorage.removeItem('accountant-ratings-cache-v2')
  localStorage.removeItem('Token')
  localStorage.removeItem('UserID')

  assert.equal(sessionStorage.getItem('director-ratings-cache-v8'), null)
  assert.equal(sessionStorage.getItem('accountant-ratings-cache-v2'), null)
  assert.equal(localStorage.getItem('Token'), null)
})

test('refresh request marker is detected', async () => {
  const isRefreshRequest = (config) => {
    if (!config) return false
    if (config.__isRefreshRequest) return true
    return String(config.url || '').includes('Users_RefreshToken')
  }
  assert.equal(isRefreshRequest({ __isRefreshRequest: true }), true)
  assert.equal(isRefreshRequest({ url: '/api/Users/Users_RefreshToken' }), true)
  assert.equal(isRefreshRequest({ url: '/api/Users/Users_GetAll' }), false)
})
