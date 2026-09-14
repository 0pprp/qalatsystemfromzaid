import { salesGatewayBase } from '@/composables/useSalesBranches'
import { getToken } from '@/services/tokenService'

function authHeaders() {
  const token = getToken()

  return token
    ? { Authorization: `Bearer ${token}`, Accept: 'application/json' }
    : { Accept: 'application/json' }
}

/**
 * Create Global Sales Manager via BE_SalesEmployee Gateway (fan-out).
 */
export async function createGlobalSalesManager(form) {
  const url = `${salesGatewayBase()}sales-management/global-managers`
  const payload = {
    userName: form.userName,
    email: form.email,
    password: form.password,
    phoneNumber: form.phoneNumber,
    address: form.address,
    userImage: form.userImage || undefined,
  }

  const res = await fetch(url, {
    method: 'POST',
    headers: {
      ...authHeaders(),
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(payload),
  })

  let data = null
  try {
    data = await res.json()
  }
  catch {
    data = null
  }

  return {
    ok: res.ok || res.status === 207,
    status: res.status,
    data: data || {},
  }
}
