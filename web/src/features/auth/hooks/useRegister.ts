import type { FormEvent } from 'react';
import { useState } from 'react';
import { ApiError } from '../../../shared/api/http';
import { authApi } from '../api/authApi';
import { useAuth } from '../context/authContext';

import { useAuthReturn } from './useAuthReturn';

export function useRegister() {
  const { signIn } = useAuth();
  const { returnState, complete } = useAuthReturn();

  const [email, setEmail] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setIsSubmitting(true);

    try {
      signIn(await authApi.register({ email, password, displayName }));
      complete();
    } catch (caught) {
      if (
        caught instanceof ApiError &&
        caught.code === 'EmailAlreadyRegistered'
      ) {
        setError('此 Email 已經註冊過了，請直接登入，或改用當初的登入方式。');
      } else {
        setError(
          caught instanceof ApiError
            ? caught.message
            : '註冊失敗，請稍後再試。',
        );
      }
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
    displayName,
    setDisplayName,
  };
}
