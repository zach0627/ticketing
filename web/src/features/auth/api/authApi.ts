import { apiGet, apiPost } from '../../../shared/api/http';
import type { AuthResponse } from '../model/AuthResponse';
import type { LoginRequest } from '../model/LoginRequest';
import type { RegisterRequest } from '../model/RegisterRequest';
import type { UserDto } from '../model/UserDto';

export const authApi = {
  register: (body: RegisterRequest) =>
    apiPost<AuthResponse>('/auth/register', body),

  login: (body: LoginRequest) => apiPost<AuthResponse>('/auth/login', body),

  /** `idToken` 就是 Google 按鈕 onSuccess 給的 `credential`。 */
  google: (idToken: string) =>
    apiPost<AuthResponse>('/auth/google', { idToken }),

  me: (signal?: AbortSignal) => apiGet<UserDto>('/me', signal),
};
