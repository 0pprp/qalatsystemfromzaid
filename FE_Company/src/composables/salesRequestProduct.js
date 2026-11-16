/**
 * The product a customer is interested in lives in SalesRequests.Notes as a labelled line:
 *  - Excel import:          «نوع المبيع: …» (the Excel «نوع المبيع» column is the product)
 *  - قسم التسويق / CRM:      «منتج CRM: …»
 *  - agents' app:           «منتج تطبيق المروّجين: …»
 *  - typed by hand:         «المنتج: …»
 */
const PRODUCT_LINE = /^\s*(?:نوع المبيع|المنتج|منتج CRM|منتج تطبيق المروّجين|منتج تطبيق المروجين)\s*:\s*(.+?)\s*$/

export function requestProduct(notes) {
  for (const line of String(notes ?? '').split(/\r?\n/)) {
    const match = line.match(PRODUCT_LINE)
    if (match?.[1])
      return match[1]
  }

  return ''
}

/** Notes without the product line(s), so the product is not shown twice. */
export function notesWithoutProduct(notes) {
  return String(notes ?? '')
    .split(/\r?\n/)
    .filter(line => !PRODUCT_LINE.test(line))
    .join('\n')
    .trim()
}
