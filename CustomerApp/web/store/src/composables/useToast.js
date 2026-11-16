import { reactive } from 'vue'

export const toastState = reactive({ show: false, text: '', color: 'primary', timeout: 3500 })

export function toast(text, color = 'primary', timeout = 3500) {
  toastState.text = text
  toastState.color = color
  toastState.timeout = timeout
  toastState.show = false
  // re-open on next tick so consecutive toasts restart the timer
  setTimeout(() => (toastState.show = true), 10)
}
export const toastSuccess = (t) => toast(t, 'success')
export const toastError = (t) => toast(t, 'error', 5000)
