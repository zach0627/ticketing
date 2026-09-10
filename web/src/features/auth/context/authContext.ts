import { createContext, use } from 'react';
import type { AuthResponse } from '../model/AuthResponse';
import type { UserDto } from '../model/UserDto';

export interface AuthContextValue {
  user: UserDto | null;
  /** 還在用既有 token 問「我是誰」——這段時間不要急著把人導去登入頁。 */
  isRestoring: boolean;
  signIn: (response: AuthResponse) => void;
  signOut: () => void;
}

/**
 * context 與 hook 放在**不含元件的檔案**裡。
 * React Fast Refresh 只在「一個檔案只匯出元件」時能安全地熱更新，
 * 把 hook 混在 Provider 檔案裡會讓開發時的熱更新退化成整頁重載。
 */
export const AuthContext = createContext<AuthContextValue | null>(null);

export function useAuth(): AuthContextValue {
  const value = use(AuthContext);
  if (!value) throw new Error('useAuth 必須在 AuthProvider 之內使用');
  return value;
}
