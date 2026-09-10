import { GoogleLogin } from '@react-oauth/google';
import { useGoogleSignIn } from '../hooks/useGoogleSignIn';
import { googleClientId } from '../model/googleConfig';

/**
 * Google 登入按鈕。
 *
 * 流程：按鈕 → Google 彈窗 → `onSuccess` 拿到 `credential`（就是 ID token）
 * → 送給我們的 `/auth/google` → 後端驗簽章與 audience → 回我們自己的 JWT。
 *
 * 前端從頭到尾**沒有 client secret**，也不需要——我們只驗證 Google 簽過的東西，
 * 不代表使用者去 Google 換 access token（設計文件 05 第 2 節）。
 */
export function GoogleSignInButton({
  onSuccess,
  onFailure,
}: {
  onSuccess: () => void;
  onFailure: (message: string) => void;
}) {
  const handleCredential = useGoogleSignIn(onSuccess, onFailure);

  // 沒設定 VITE_GOOGLE_CLIENT_ID 就不顯示按鈕，Email 登入照常可用
  if (!googleClientId) return null;

  return (
    <div className="auth-form__google">
      <span className="auth-divider">或使用 Google 帳號</span>
      <GoogleLogin
        onSuccess={handleCredential}
        onError={() => onFailure('Google 登入被取消或失敗。')}
      />
    </div>
  );
}
