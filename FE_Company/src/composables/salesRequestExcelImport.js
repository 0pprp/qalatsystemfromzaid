/**
 * Excel → SalesRequest import helpers.
 * Phone storage normalization is BE-owned (SalesPhoneNormalizer).
 * FE only maps columns and converts Excel cell values to strings.
 */

function foldAr(value) {
  return String(value ?? '')
    .normalize('NFC')
    .replace(/[\u0640\u200B\u200C\u200D\uFEFF]/g, '')
    .replace(/[\u00A0\u202F\u2007]/g, ' ')
    .trim()
    .replace(/[أإآٱ]/g, 'ا')
    .replace(/ة/g, 'ه')
    .replace(/ى/g, 'ي')
    .replace(/\s+/g, ' ')
}

export function excelHeaderKey(value) {
  const t = foldAr(value).replace(/[_\-]/g, ' ').toLowerCase()
  if (['اسم الزبون', 'اسم العميل', 'customername', 'fullname', 'name', 'الاسم'].includes(t))
    return 'name'
  if (
    t === 'الهاتف'
    || t === 'هاتف'
    || t === 'رقم الهاتف'
    || t === 'رقم هاتف'
    || t === 'رقم الجوال'
    || t === 'الجوال'
    || t === 'موبايل'
    || t === 'الموبايل'
    || t === 'التلفون'
    || t === 'phone'
    || t === 'phonenumber'
    || t === 'phone number'
    || t === 'mobile'
    || t === 'customerphone'
    || t === 'customer phone'
  )
    return 'phone'
  if (t === 'المحافظة' || t === 'المحافظه' || t === 'المدينة' || t === 'المدينه' || ['province', 'city', 'governorate'].includes(t))
    return 'province'
  if (['العنوان', 'address'].includes(t))
    return 'address'
  if (['نوع المبيع', 'نوع البيع', 'saletype', 'sale type'].includes(t))
    return 'saleType'

  return ''
}

/** Convert Excel cell to string for BE; no Iraq phone inventing here. */
export function excelCellText(value) {
  if (value == null || value === '')
    return ''
  if (typeof value === 'number' && Number.isFinite(value)) {
    // Numeric phone cells lose leading zero in Excel — send integer digits to BE.
    if (Number.isInteger(value) || Math.abs(value - Math.trunc(value)) < 1e-9)
      return String(Math.trunc(value))

    // Scientific / float — invariant string; BE DigitsOnly handles E-notation.
    return String(value)
  }

  return String(value).trim()
}

/**
 * @param {ArrayBuffer} buffer
 * @param {object} options
 * @param {typeof import('xlsx')} options.XLSX
 * @param {(provinceText: string) => ({ cityValue: string, cityName: string } | null)} options.resolveCity
 */
export function parseSalesRequestExcel(buffer, { XLSX, resolveCity }) {
  const workbook = XLSX.read(buffer, { type: 'array' })
  const sheet = workbook.Sheets[workbook.SheetNames[0]]
  if (!sheet)
    throw new Error('الملف لا يحتوي على ورقة')

  // raw:true keeps numeric phones as numbers (not locale/scientific display strings).
  const rows = XLSX.utils.sheet_to_json(sheet, { header: 1, defval: '', raw: true })
  if (!rows.length)
    throw new Error('الملف فارغ')

  const map = {}
  ;(rows[0] || []).forEach((header, index) => {
    const key = excelHeaderKey(header)
    if (key && map[key] == null)
      map[key] = index
  })
  if (map.name == null)
    throw new Error('عمود اسم الزبون مطلوب في الصف الأول')
  if (map.phone == null)
    throw new Error('عمود الهاتف مطلوب في الصف الأول')

  const valid = []
  const errors = []
  let total = 0
  for (let i = 1; i < rows.length; i++) {
    const row = rows[i] || []
    const name = excelCellText(row[map.name])
    const phone = excelCellText(row[map.phone])
    const province = excelCellText(map.province != null ? row[map.province] : '')
    const address = excelCellText(map.address != null ? row[map.address] : '')
    const saleType = excelCellText(map.saleType != null ? row[map.saleType] : '')
    if (![name, phone, province, address, saleType].some(Boolean))
      continue
    total++
    const excelRow = i + 1
    if (!name) {
      errors.push({ rowNumber: excelRow, message: 'اسم الزبون مطلوب' })
      continue
    }
    if (!phone) {
      errors.push({ rowNumber: excelRow, message: 'رقم الهاتف مطلوب' })
      continue
    }
    if (!province) {
      errors.push({ rowNumber: excelRow, message: 'المحافظة مطلوبة' })
      continue
    }
    const city = resolveCity(province)
    if (!city) {
      errors.push({
        rowNumber: excelRow,
        message: `المحافظة غير معروفة بعد التطبيع: ${province}`,
      })
      continue
    }
    valid.push({
      rowNumber: excelRow,
      customerName: name,
      phone,
      province: province || city.cityName,
      address,
      saleType,
      cityValue: city.cityValue,
      cityName: city.cityName,
    })
  }

  return { total, valid, errors }
}
