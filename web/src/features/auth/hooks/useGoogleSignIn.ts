import { ApiError } from '../../../shared/api/http';
import { authApi } from '../api/authApi';
import { useAuth } from '../context/authContext';

/**
 * Google 登入按鈕。
 *
 * 流程：按鈕 → Google 彈窗 → `onSuccess` 拿到 `credential`（就是 ID token）
 * → 送給我們的 `/auth/google` → 後端驗簽章與 audience → 回我們自己的 JWT。
 *
 * 前端從頭到尾**沒有 client secret**，也不需要——我們只驗證 Google 簽過的東西，
 * 不代表使用者去 Google 換 access token（設計文件 05 第 2 節）。
 */
import type { CredentialResponse } from '@react-oauth/google';

export function useGoogleSignIn(
  onSuccess: () => void,
  onFailure: (message: string) => void,
) {
  const { signIn } = useAuth();
  const handleCredential = async (credentialResponse: CredentialResponse) => {
    const idToken = credentialResponse.credential;
    if (!idToken) {
      onFailure('Google 沒有回傳憑證，請再試一次。');
      return;
    }

    try {
      signIn(await authApi.google(idToken));
      onSuccess();
    } catch (error) {
      onFailure(
        error instanceof ApiError
          ? error.message
          : 'Google 登入失敗，請稍後再試。',
      );
    }
  };
  return handleCredential;
}
