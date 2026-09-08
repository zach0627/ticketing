import { apiGet, apiPost } from '../../shared/api/http';
import type { AuthResponse, LoginRequest, RegisterRequest, UserDto } from '../../shared/api/types';

export const authApi = {
  register: (body: RegisterRequest) => apiPost<AuthResponse>('/auth/register', body),

  login: (body: LoginRequest) => apiPost<AuthResponse>('/auth/login', body),

  /** `idToken` 就是 Google 按鈕 onSuccess 給的 `credential`。 */
  google: (idToken: string) => apiPost<AuthResponse>('/auth/google', { idToken }),

  me: (signal?: AbortSignal) => apiGet<UserDto>('/me', signal),
};
