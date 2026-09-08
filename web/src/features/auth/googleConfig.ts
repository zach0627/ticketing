/**
 * Google OAuth 用戶端 ID。**公開值**，會出現在前端原始碼裡（設計文件 05 第 6 節）。
 * 沒設定時整個 Google 登入路徑會安靜地消失，Email 登入不受影響。
 */
export const googleClientId: string | undefined = import.meta.env.VITE_GOOGLE_CLIENT_ID;
