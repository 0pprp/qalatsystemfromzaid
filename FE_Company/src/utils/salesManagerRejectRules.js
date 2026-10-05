/**
 * Pure rules for sales-manager request reject CTA.
 * Sent tab = Employee/Follower/Delegate submitted + New.
 */
export function canManagerRejectRequest(row, {
  status,
  isEmployeeSubmitted,
  isFollowerSubmitted,
  isDelegateSubmitted,
}) {
  if (!row)
    return false
  const s = status || ''
  if (s === 'Completed' || s === 'Rejected')
    return false
  if (s === 'Inspected')
    return true
  if (isEmployeeSubmitted || isFollowerSubmitted || isDelegateSubmitted)
    return true

  return s === 'Assigned' || s === 'Viewed' || s === 'Returned' || s === 'Pending'
    || s === 'PreparedForSale' || s === 'InProgress' || s === 'ConvertedToSale' || s === 'New'
}
