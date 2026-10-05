export function ratingChipColor(rating) {
  switch (rating) {
  case 'ممتاز':
    return 'success'
  case 'جيد':
    return 'info'
  case 'ضعيف':
    return 'warning'
  case 'متلكئ':
  case 'مرفوض': // legacy label alias
    return 'orange-darken-2'
  case 'سيء':
    return 'error'
  default:
    return 'grey'
  }
}

export function riskChipColor(risk) {
  switch (risk) {
  case 'None':
    return 'grey'
  case 'Low':
    return 'info'
  case 'Medium':
    return 'warning'
  case 'High':
    return 'error'
  default:
    return 'grey'
  }
}

export function riskLabelAr(risk) {
  switch (risk) {
  case 'None':
    return 'لا خطر'
  case 'Low':
    return 'منخفض'
  case 'Medium':
    return 'متوسط'
  case 'High':
    return 'مرتفع'
  default:
    return risk || '—'
  }
}
