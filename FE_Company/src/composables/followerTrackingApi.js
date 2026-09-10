import axios from 'axios'
import { getAuthHeaders } from '@/services/tokenService'

function followerTrackingBase() {
  return `${localStorage.getItem('LinkCity') || ''}follower-tracking/`
}

export function followerRowKey(row) {
  return String(row.followerId ?? row.FollowerId ?? '')
}

export function normalizeFollowerRows(data) {
  const list = Array.isArray(data)
    ? data
    : Array.isArray(data?.data)
      ? data.data
      : Array.isArray(data?.followers)
        ? data.followers
        : []

  return list.map(row => ({
    followerId: row.followerId ?? row.FollowerId,
    followerName: row.followerName || row.FollowerName || '',
  }))
}

export function normalizeLiveRows(data) {
  const list = Array.isArray(data)
    ? data
    : Array.isArray(data?.data)
      ? data.data
      : []

  return list.map(row => ({
    followerId: row.followerId ?? row.FollowerId,
    followerName: row.followerName || row.FollowerName || '',
    shiftId: row.shiftId ?? row.ShiftId,
    shiftStatus: row.shiftStatus || row.ShiftStatus || 'Active',
    lastLatitude: row.latitude ?? row.Latitude,
    lastLongitude: row.longitude ?? row.Longitude,
    lastAccuracy: row.accuracy ?? row.Accuracy,
    lastLocationAt: row.capturedAtUtc ?? row.CapturedAtUtc ?? row.updatedAtUtc ?? row.UpdatedAtUtc,
    locationStatus: row.locationStatus || row.LocationStatus || 'Live',
  }))
}

export const shiftStatusLabel = {
  Active: 'على الدوام',
  Closed: 'منتهي',
}

export const locationStatusLabel = {
  Live: 'مباشر',
  Stale: 'الموقع غير محدث',
  Offline: 'بدون اتصال',
  NoLocation: 'بدون موقع',
  NoShift: 'بدون دوام',
}

export async function ftGet(path) {
  const { data } = await axios.get(`${followerTrackingBase()}${path}`, { headers: getAuthHeaders() })

  return data
}

export async function ftGetLiveLocations() {
  return normalizeLiveRows(await ftGet('live'))
}

export async function ftGetFollowers() {
  return normalizeFollowerRows(await ftGet('followers'))
}

export async function ftGetRoute(followerId, date) {
  return ftGet(`followers/${followerId}/route?date=${encodeURIComponent(date)}`)
}
