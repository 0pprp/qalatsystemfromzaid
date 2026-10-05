# -*- coding: utf-8 -*-
from pathlib import Path

p = Path(r"C:\GitHub\SalesHaider-main\SalesHaider-main\FE_Company\src\pages\accountant-ratings.vue")
t = p.read_text(encoding="utf-8")

old_load = """async function loadCities() {
  const cached = cache.getCities()
  if (cached.length)
    gatewayCities.value = cached
  try {
    gatewayCities.value = await accountantCities() || []
    cache.setCities(gatewayCities.value)
  }
  catch {
    if (!gatewayCities.value.length)
      gatewayCities.value = []
  }
  await fetchCities()
}

async function warmMonthStats() {
  hubMonthBusy.value = true
  warmBusy.value = true
  try {
    const raw = await accountantStatsBranchMonth()
    const rows = Array.isArray(raw) ? raw : (raw ? [raw] : [])
    const map = {}
    for (const row of rows) {
      const key = String(row.cityValue ?? row.CityValue ?? '').trim()
      if (key)
        map[key] = row
    }
    hubMonthByCity.value = map
    cache.setMonthMap(map)
    if (selected.value?.value && map[selected.value.value])
      detailMonth.value = map[selected.value.value]
  }
  catch {
    /* keep prior cache */
  }
  finally {
    hubMonthBusy.value = false
    warmBusy.value = false
  }
}

async function loadDetailMonth(cityValue) {
  if (!cityValue)
    return
  const hit = cache.getMonth(cityValue) || hubMonthByCity.value[cityValue]
  if (hit) {
    detailMonth.value = hit

    return
  }
  detailMonthBusy.value = true
  try {
    const row = await accountantStatsBranchMonth(cityValue)
    detailMonth.value = row
    cache.setMonth(cityValue, row)
    hubMonthByCity.value = { ...hubMonthByCity.value, [cityValue]: row }
  }
  catch {
    detailMonth.value = null
  }
  finally {
    detailMonthBusy.value = false
  }
}

async function loadCatalog(cityValue) {
  const cached = cache.getCatalog(cityValue)
  if (cached) {
    catalog.value = cached

    return
  }
  catalogBusy.value = true
  try {
    const raw = await accountantStatsCatalog(cityValue)
    const mapped = {
      lists: normalizeEntities(raw?.lists || raw?.Lists),
      followers: normalizeEntities(raw?.followers || raw?.Followers),
      employees: normalizeEntities(raw?.employees || raw?.Employees),
    }"""

new_load = """function currentBranchCity() {
  const name = String(localStorage.getItem('CityName') || '').trim() || 'المحافظة الحالية'
  const value = String(localStorage.getItem('Database') || name).trim() || 'current'
  return { value, name }
}

async function loadCities() {
  await fetchCities()
  const cur = currentBranchCity()
  const hit = (branches.value || []).find(b =>
    String(b.database || b.Database || '') === cur.value
    || String(b.name || b.Name || '') === cur.name
    || String(b.value || b.Value || '') === cur.value)
  const card = hit
    ? { value: String(hit.value ?? hit.Value ?? cur.value), name: hit.name || hit.Name || cur.name }
    : cur
  gatewayCities.value = [card]
  cache.setCities(gatewayCities.value)
}

async function warmMonthStats() {
  hubMonthBusy.value = true
  warmBusy.value = true
  try {
    const cur = currentBranchCity()
    const key = String(gatewayCities.value[0]?.value || cur.value)
    const raw = await accountantStatsBranchMonth()
    const map = { [key]: raw }
    hubMonthByCity.value = map
    cache.setMonthMap(map)
    if (selected.value?.value && map[selected.value.value])
      detailMonth.value = map[selected.value.value]
    else if (!selected.value)
      detailMonth.value = raw
  }
  catch {
    /* keep prior cache */
  }
  finally {
    hubMonthBusy.value = false
    warmBusy.value = false
  }
}

async function loadDetailMonth(cityValue) {
  if (!cityValue)
    return
  const hit = cache.getMonth(cityValue) || hubMonthByCity.value[cityValue]
  if (hit) {
    detailMonth.value = hit

    return
  }
  detailMonthBusy.value = true
  try {
    const row = await accountantStatsBranchMonth()
    detailMonth.value = row
    cache.setMonth(cityValue, row)
    hubMonthByCity.value = { ...hubMonthByCity.value, [cityValue]: row }
  }
  catch {
    detailMonth.value = null
  }
  finally {
    detailMonthBusy.value = false
  }
}

async function loadCatalog(cityValue) {
  const cached = cache.getCatalog(cityValue)
  if (cached) {
    catalog.value = cached

    return
  }
  catalogBusy.value = true
  try {
    const raw = await accountantStatsCatalog()
    const mapped = {
      lists: normalizeEntities(raw?.lists || raw?.Lists),
      followers: normalizeEntities(raw?.followers || raw?.Followers),
      employees: [],
    }"""

if old_load not in t:
    raise SystemExit("old_load block not found")
t = t.replace(old_load, new_load)

old_prof = """function openCustomerProfile(c) {
  const city = String(selected.value?.value || '').trim()
  if (!city) {
    bucketError.value = 'تعذر تحديد المحافظة للزبون'

    return
  }
  saveAccountantReturnState()
  router.push({
    path: '/sales-manager-customer-profile',
    query: {
      cityValue: city,
      customerId: c.customerId || '',
      name: c.customerName || '',
      phone: c.phone || '',
    },
  })
}"""

new_prof = """function openCustomerProfile(c) {
  const id = Number(c.customerId || c.CustomerId || 0)
  if (!id) {
    bucketError.value = 'تعذر تحديد الزبون'

    return
  }
  saveAccountantReturnState()
  router.push({
    path: '/customer-profile',
    query: { customerID: id },
  })
}"""

if old_prof not in t:
    raise SystemExit("openCustomerProfile not found")
t = t.replace(old_prof, new_prof)

t = t.replace(
    """  else if (scope.value === 'employee' && entityId.value > 0)
    loadTrend({ useCacheFirst: true })
  else if (scope.value === 'branch')""",
    """  else if (scope.value === 'branch')""",
)

t = t.replace(
    """  if (scope.value === 'employee')
    return catalog.value.employees || []

  return []
}""",
    """  return []
}""",
)

t = t.replace(
    """  if (scope.value === 'employee')
    return entityId.value > 0
  if (scope.value === 'follower')""",
    """  if (scope.value === 'follower')""",
)

old_mount = """onMounted(async () => {
  mountedOnce = true
  cache.reloadFromSession()
  await loadCities()
  const restored = await restoreAccountantReturnState()
  if (!restored)
    applyRouteCity()
  void warmMonthStats()
  void /* no warm */ void()
})"""

new_mount = """onMounted(async () => {
  mountedOnce = true
  cache.reloadFromSession()
  await loadCities()
  const restored = await restoreAccountantReturnState()
  if (!restored) {
    const qCity = String(route.query.city || '').trim()
    if (qCity)
      applyRouteCity()
    else if (gatewayCities.value[0])
      openCity(gatewayCities.value[0], { pushHistory: true })
  }
  void warmMonthStats()
})"""

if old_mount not in t:
    raise SystemExit("onMounted not found")
t = t.replace(old_mount, new_mount)

# hide hub back if we auto-open — keep UI
p.write_text(t, encoding="utf-8")
print("ok")
print("accountantCities", "accountantCities" in t)
print("employee chip", "موظفو المبيعات" in t)
print("broken warm", "void /* no warm */" in t)
print("customer-profile", "/customer-profile" in t)
