import { useQueryClient } from '@tanstack/react-query';
import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  ApiError,
  setUnauthorizedHandler,
  tokenStore,
} from '../../../shared/api/http';
import { authApi } from '../api/authApi';
import type { AuthContextValue } from '../context/authContext';
import type { AuthResponse } from '../model/AuthResponse';
import type { UserDto } from '../model/UserDto';

export function useAuthSession() {
  const queryClient = useQueryClient();
  const [user, setUser] = useState<UserDto | null>(null);
  const [isRestoring, setIsRestoring] = useState(
    () => tokenStore.read() !== null,
  );

  const signOut = useCallback(() => {
    tokenStore.clear();
    setUser(null);
    // 換人或登出一定要清快取：訂單、保留這些是私人資料，
    // 留在 React Query 裡會被下一個帳號看到（設計文件 13 第 4 節）。
    queryClient.clear();
  }, [queryClient]);

  const signIn = useCallback(
    (response: AuthResponse) => {
      queryClient.clear();
      tokenStore.write(response.accessToken);
      setUser(response.user);
    },
    [queryClient],
  );

  // 受保護請求收到 401（token 過期或被撤銷）時，由 http.ts 呼回這裡
  useEffect(() => setUnauthorizedHandler(signOut), [signOut]);

  // 重新整理後用既有 token 問一次 /me：token 還有效就恢復登入狀態
  useEffect(() => {
    if (tokenStore.read() === null) return;

    const controller = new AbortController();

    authApi
      .me(controller.signal)
      .then((me) => setUser(me))
      .catch((error: unknown) => {
        // 只有「伺服器明確拒絕」才清 token。
        // StrictMode 在開發模式會把 effect 跑兩次，第一次的 abort 不算失敗——
        // 把 abort 當成失敗會在開發時莫名其妙被登出。
        if (error instanceof ApiError) signOut();
      })
      .finally(() => {
        if (!controller.signal.aborted) setIsRestoring(false);
      });

    return () => controller.abort();
  }, [signOut]);

  const value = useMemo<AuthContextValue>(
    () => ({ user, isRestoring, signIn, signOut }),
    [user, isRestoring, signIn, signOut],
  );

  return value;
}
