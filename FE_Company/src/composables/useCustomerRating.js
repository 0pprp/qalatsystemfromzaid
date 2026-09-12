export function ratingChipColor(rating) {
  switch (rating) {
  case 'ممتاز':
    return 'success'
  case 'جيد':
    return 'info'
  case 'ضعيف':
    return 'warning'
  case 'مرفوض':
    return 'error'
  case 'قانونية':
    return 'error'
  default:
    return 'secondary'
  }
}

export function riskChipColor(risk) {
  switch (risk) {
  case 'None':
    return 'secondary'
  case 'Low':
    return 'info'
  case 'Medium':
    return 'warning'
  case 'High':
    return 'error'
  default:
    return 'secondary'
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
