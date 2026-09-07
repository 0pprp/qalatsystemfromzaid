function pick(obj, ...keys) {
  if (!obj)
    return undefined
  for (const key of keys) {
    if (obj[key] != null && obj[key] !== '')
      return obj[key]
  }

  return undefined
}

export function documentTypeOf(doc) {
  return String(pick(doc, 'type', 'Type', 'documentType', 'DocumentType') || '')
}

export function isCombinedSaleDocument(doc) {
  const type = documentTypeOf(doc)

  return type === 'SaleDocuments' || type === 'PreviewSaleDocuments'
}

export function saleDisplayDocuments(sale) {
  const rows = pick(sale, 'documents', 'Documents') || []
  const list = Array.isArray(rows) ? rows : []
  const combined = list.filter(isCombinedSaleDocument)
  if (combined.length)
    return combined.slice(0, 1)

  return list.filter(doc => {
    const type = documentTypeOf(doc)

    return type === 'Contract' || type === 'PromissoryNote' || type === 'PreviewContract' || type === 'PreviewPromissoryNote'
  })
}

export function saleDocumentTitle(doc) {
  if (isCombinedSaleDocument(doc))
    return 'عقد البيع + وصل الأمانة'

  const type = documentTypeOf(doc)

  return type === 'PromissoryNote' || type === 'PreviewPromissoryNote' ? 'وصل الأمانة' : 'عقد البيع'
}
