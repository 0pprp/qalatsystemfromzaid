function pick(obj, ...keys) {
  if (!obj)
    return undefined
  for (const key of keys) {
    if (obj[key] != null && obj[key] !== '')
      return obj[key]
  }

  return undefined
}

export function isCombinedSaleDocument(doc) {
  const type = String(pick(doc, 'type', 'Type') || '')

  return type === 'SaleDocuments' || type === 'PreviewSaleDocuments'
}

export function saleDisplayDocuments(sale) {
  const rows = pick(sale, 'documents', 'Documents') || []
  const list = Array.isArray(rows) ? rows : []
  const combined = list.filter(isCombinedSaleDocument)
  if (combined.length)
    return combined

  return list.filter(doc => {
    const type = String(pick(doc, 'type', 'Type') || '')

    return type === 'Contract' || type === 'PromissoryNote' || type === 'PreviewContract' || type === 'PreviewPromissoryNote'
  })
}

export function saleDocumentTitle(doc) {
  if (isCombinedSaleDocument(doc))
    return 'عقد البيع + وصل الأمانة'

  const type = String(pick(doc, 'type', 'Type') || '')

  return type === 'PromissoryNote' || type === 'PreviewPromissoryNote' ? 'وصل الأمانة' : 'عقد البيع'
}
