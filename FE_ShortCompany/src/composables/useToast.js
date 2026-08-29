import { ref } from 'vue'

const state = ref({
  visible: false,
  message: '',
  color: 'success',
  timeout: 3000,
})

const dialogState = ref({
  visible: false,
  title: '',
  message: '',
  type: 'info', // success, error, warning, info
  showCancel: false,
})

const dialogCallback = ref(null)

export function useToast() {
  
  // --- Snackbar Methods ---
  function showToast(message, options = {}) {
    state.value.message = message
    state.value.color = options.color || 'success'
    state.value.timeout = options.timeout || 3000
    state.value.visible = true
  }

  function success(message) { showToast(message, { color: 'success' }) }
  function error(message) { showToast(message, { color: 'error' }) }
  function info(message) { showToast(message, { color: 'info' }) }
  function warning(message) { showToast(message, { color: 'warning' }) }

  // --- Dialog Methods (Professional Alert) ---
  function alert(title, msg, type = 'info') {
    dialogState.value = {
      visible: true,
      title: title || '',
      message: msg || '',
      type: type,
      showCancel: false,
    }
    dialogCallback.value = null
  }

  function alertSuccess(msg, title = 'تمت العملية بنجاح') {
    alert(title, msg, 'success')
  }

  function alertError(msg, title = 'خطأ') {
    alert(title, msg, 'error')
  }
  
  function confirm(title, msg, onConfirm) {
    dialogState.value = {
      visible: true,
      title: title,
      message: msg,
      type: 'warning',
      showCancel: true,
    }
    dialogCallback.value = onConfirm
  }

  function closeDialog() {
    dialogState.value.visible = false
  }

  return {
    state,
    success,
    error,
    info,
    warning,
    
    // Dialog exports
    dialogState,
    alert,
    alertSuccess,
    alertError,
    confirm,
    closeDialog,
    onConfirm: dialogCallback,
  }
}
