import assert from 'node:assert/strict'
import { canManagerRejectRequest } from './salesManagerRejectRules.js'

assert.equal(canManagerRejectRequest({}, {
  status: 'New',
  isEmployeeSubmitted: false,
  isFollowerSubmitted: true,
  isDelegateSubmitted: false,
}), true)

assert.equal(canManagerRejectRequest({}, {
  status: 'New',
  isEmployeeSubmitted: false,
  isFollowerSubmitted: false,
  isDelegateSubmitted: true,
}), true)

assert.equal(canManagerRejectRequest({}, {
  status: 'Completed',
  isEmployeeSubmitted: true,
  isFollowerSubmitted: false,
  isDelegateSubmitted: false,
}), false)

console.log('salesManagerRejectRules: ok')
