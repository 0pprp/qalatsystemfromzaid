/**
 * رابط الفيديو التعليمي للمحاسب الرئيسي.
 * عبّه هنا أو عبر VITE_ACCOUNTANT_TUTORIAL_VIDEO_URL أو جدول MobileAppTutorialLinks (appKey=accountant).
 */
export const ACCOUNTANT_TUTORIAL_APP_KEY = 'accountant'

export const ACCOUNTANT_TUTORIAL_VIDEO_URL = String(
  import.meta.env.VITE_ACCOUNTANT_TUTORIAL_VIDEO_URL || '',
).trim()
