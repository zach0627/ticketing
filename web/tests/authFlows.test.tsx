import type { FormEvent } from 'react';
import { act, cleanup, renderHook } from '@testing-library/react';
import { useLocation } from 'react-router';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';
import { useLogin } from '../src/features/auth/hooks/useLogin';
import { useRegister } from '../src/features/auth/hooks/useRegister';
import { authApi } from '../src/features/auth/api/authApi';
import { ApiError } from '../src/shared/api/http';
import { buyer, testProviders } from './support/TestProviders';

vi.mock('../src/features/auth/api/authApi', () => ({
  authApi: { login: vi.fn(), register: vi.fn() },
}));
const event = () => ({ preventDefault: vi.fn() }) as unknown as FormEvent;
beforeEach(() => vi.resetAllMocks());
afterEach(cleanup);

test('login sends entered credentials and returns to the same seat draft', async () => {
  const seatDraft = { performanceId: 1, seatIds: [101] };
  const providers = testProviders({
    entry: '/login',
    state: { from: '/performances/1/seats', seatDraft },
  });
  const response = { accessToken: 'test-access-token', user: buyer };
  vi.mocked(authApi.login).mockResolvedValue(response);
  const { result } = renderHook(
    () => ({ flow: useLogin(), location: useLocation() }),
    providers,
  );
  act(() => {
    result.current.flow.setEmail('buyer@example.test');
    result.current.flow.setPassword('test-password');
  });
  await act(() => result.current.flow.handleSubmit(event()));
  expect(authApi.login).toHaveBeenCalledExactlyOnceWith({
    email: 'buyer@example.test',
    password: 'test-password',
  });
  expect(providers.auth.signIn).toHaveBeenCalledWith(response);
  expect(result.current.location.pathname).toBe('/performances/1/seats');
  expect(result.current.location.state).toEqual({ seatDraft });
});

test('incorrect credentials stay on login and do not trigger global sign-out', async () => {
  vi.mocked(authApi.login).mockRejectedValue(
    new ApiError(401, 'InvalidCredentials', '帳號或密碼錯誤'),
  );
  const providers = testProviders({ entry: '/login' });
  const { result } = renderHook(
    () => ({ flow: useLogin(), location: useLocation() }),
    providers,
  );
  await act(() => result.current.flow.handleSubmit(event()));
  expect(result.current.flow.error).toBe('帳號或密碼錯誤');
  expect(result.current.flow.isSubmitting).toBe(false);
  expect(result.current.location.pathname).toBe('/login');
  expect(providers.auth.signOut).not.toHaveBeenCalled();
});

test('registration preserves duplicate-email feedback and rejects external return paths', async () => {
  vi.mocked(authApi.register).mockRejectedValueOnce(
    new ApiError(409, 'EmailAlreadyRegistered', 'duplicate'),
  );
  const { result } = renderHook(
    () => ({ flow: useRegister(), location: useLocation() }),
    testProviders({
      entry: '/register',
      state: { from: '//untrusted.example' },
    }),
  );
  await act(() => result.current.flow.handleSubmit(event()));
  expect(result.current.flow.error).toBe(
    '此 Email 已經註冊過了，請直接登入，或改用當初的登入方式。',
  );
  vi.mocked(authApi.register).mockResolvedValueOnce({
    accessToken: 'test-access-token',
    user: buyer,
  });
  await act(() => result.current.flow.handleSubmit(event()));
  expect(result.current.location.pathname).toBe('/');
});
