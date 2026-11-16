import { defineStore } from 'pinia'
import { authApi, meApi, notificationsApi } from '@/api'
import { tokens } from '@/api/tokens'
import { readJson, writeJson } from '@/utils/storage'

const USER_KEY = 'qa.user'
const REFERRAL_KEY = 'qa.referralCode'
const POLL_MS = 60000

export const useAuthStore = defineStore('auth', {
  state: () => ({
    user: readJson(USER_KEY, null),
    hasToken: !!tokens.access || !!tokens.refresh,
    ready: false,
    unreadCount: 0,
    // login dialog
    loginDialog: false,
    loginRedirect: null, // route location to go to after login
    loginReason: '',
    _pollTimer: null,
  }),
  getters: {
    isLoggedIn: (s) => s.hasToken && !!s.user,
    pendingReferralCode: () => {
      try { return localStorage.getItem(REFERRAL_KEY) || '' } catch { return '' }
    },
  },
  actions: {
    async init() {
      tokens.onExpired(() => this.clearSession())
      if (this.hasToken) {
        try {
          await this.fetchMe()
        } catch {
          // offline or expired: interceptor handles 401 → clearSession
        }
      }
      this.ready = true
      if (this.isLoggedIn) this.startPolling()
    },
    async fetchMe() {
      const me = await meApi.get()
      this.setUser(me)
      return me
    },
    setUser(me) {
      this.user = me
      writeJson(USER_KEY, me)
    },
    requestOtp(phone) {
      return authApi.requestOtp(phone)
    },
    /** body per contract §2 verify */
    async verifyOtp(body) {
      const res = await authApi.verifyOtp({
        ...body,
        device: { platform: 'web', appVersion: '1.0.0' },
      })
      tokens.set(res)
      this.hasToken = true
      this.setUser(res.user)
      this.clearReferralCode() // used (registration) or irrelevant (existing account)
      this.startPolling()
      return res
    },
    async logout() {
      const refreshToken = tokens.refresh
      try {
        if (refreshToken) await authApi.logout(refreshToken)
      } catch { /* ignore */ }
      this.clearSession()
    },
    clearSession() {
      tokens.clear()
      this.hasToken = false
      this.user = null
      this.unreadCount = 0
      writeJson(USER_KEY, null)
      this.stopPolling()
    },
    // ---- referral code captured from /r/:code ----
    storeReferralCode(code) {
      try { localStorage.setItem(REFERRAL_KEY, String(code).trim().toUpperCase()) } catch { /* ignore */ }
    },
    clearReferralCode() {
      try { localStorage.removeItem(REFERRAL_KEY) } catch { /* ignore */ }
    },
    // ---- login dialog ----
    openLogin(redirect = null, reason = '') {
      this.loginRedirect = redirect
      this.loginReason = reason
      this.loginDialog = true
    },
    // ---- notifications badge ----
    async refreshUnread() {
      if (!this.isLoggedIn) return
      try {
        const res = await notificationsApi.list(1)
        this.unreadCount = res.unreadCount ?? 0
      } catch { /* ignore */ }
    },
    startPolling() {
      this.stopPolling()
      this.refreshUnread()
      this._pollTimer = setInterval(() => {
        if (document.visibilityState !== 'hidden') this.refreshUnread()
      }, POLL_MS)
    },
    stopPolling() {
      if (this._pollTimer) clearInterval(this._pollTimer)
      this._pollTimer = null
    },
  },
})
