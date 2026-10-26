import axios from 'axios'
import { getAuthHeaders } from '@/services/tokenService'

function base() {
  const link = String(localStorage.getItem('LinkCity') || '').trim()
  if (!link)
    throw Object.assign(new Error('اختر المحافظة من تسجيل الدخول ثم أعد المحاولة'), {
      response: { status: 400, data: { message: 'اختر المحافظة من تسجيل الدخول ثم أعد المحاولة' } },
    })
  const root = link.endsWith('/') ? link : `${link}/`

  return `${root}accountant/`
}

async function accountantRequest(method, path, { data, timeoutMs } = {}) {
  try {
    const { data: body } = await axios({
      method,
      url: `${base()}${path}`,
      data,
      headers: getAuthHeaders(),
      timeout: timeoutMs ?? 60000,
    })

    return body
  }
  catch (err) {
    if (!err?.response && /network error/i.test(String(err?.message || ''))) {
      throw Object.assign(new Error('تعذر الاتصال بالخادم — تحقق من المحافظة والاتصال'), {
        response: { status: 0, data: { message: 'تعذر الاتصال بالخادم — تحقق من المحافظة والاتصال' } },
      })
    }
    throw err
  }
}

export async function accountantListWrongSales({ reviewed = false, page, pageSize } = {}) {
  const params = new URLSearchParams({
    reviewed: reviewed ? 'true' : 'false',
  })
  if (page != null)
    params.set('page', String(page))
  if (pageSize != null)
    params.set('pageSize', String(pageSize))

  return accountantRequest('get', `wrong-sales?${params}`, { timeoutMs: 90000 })
}

export async function accountantGetWrongSale(id) {
  return accountantRequest('get', `wrong-sales/${encodeURIComponent(id)}`, { timeoutMs: 60000 })
}

export async function accountantReviewWrongSale(id, note) {
  return accountantRequest('post', `wrong-sales/${encodeURIComponent(id)}/review`, {
    data: { note },
    timeoutMs: 60000,
  })
}
