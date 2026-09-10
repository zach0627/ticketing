import type { FormEvent } from 'react';
import { useState } from 'react';
import { ApiError } from '../../../shared/api/http';
import { authApi } from '../api/authApi';
import { useAuth } from '../context/authContext';

import { useAuthReturn } from './useAuthReturn';

export function useLogin() {
  const { signIn } = useAuth();
  const { returnState, complete } = useAuthReturn();

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setIsSubmitting(true);

    try {
      signIn(await authApi.login({ email, password }));
      complete();
    } catch (caught) {
      // 這裡的 401 是「帳密錯」，留在表單上顯示；
      // 不能走全域的「清 token 導登入頁」，那會變成在登入頁一直重新導向
      setError(
        caught instanceof ApiError ? caught.message : '登入失敗，請稍後再試。',
      );
    } finally {
      setIsSubmitting(false);
    }
  }

  return {
    email,
    setEmail,
    password,
    setPassword,
    error,
    setError,
    isSubmitting,
    handleSubmit,
    returnState,
    complete,
  };
}
