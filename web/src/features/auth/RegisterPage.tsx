import { useState } from 'react';
import type { FormEvent } from 'react';
import { Link, useLocation, useNavigate } from 'react-router';
import { ApiError } from '../../shared/api/http';
import { authApi } from './api';
import { useAuth } from './authContext';
import { GoogleSignInButton } from './GoogleSignInButton';
import { safeReturnPath } from './returnPath';

/** 與後端 RegisterRequest 的 DataAnnotations 對齊；真正的把關仍在後端。 */
const passwordMinLength = 8;

export function RegisterPage() {
  const { signIn } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const returnPath = safeReturnPath((location.state as { from?: unknown } | null)?.from);

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
      navigate(returnPath, { replace: true });
    } catch (caught) {
      if (caught instanceof ApiError && caught.code === 'EmailAlreadyRegistered') {
        setError('此 Email 已經註冊過了，請直接登入，或改用當初的登入方式。');
      } else {
        setError(caught instanceof ApiError ? caught.message : '註冊失敗，請稍後再試。');
      }
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <section className="auth">
      <h1>註冊</h1>

      <form className="auth-form" onSubmit={handleSubmit} noValidate>
        <label htmlFor="register-email">Email</label>
        <input
          id="register-email"
          type="email"
          autoComplete="email"
          required
          value={email}
          onChange={(e) => setEmail(e.target.value)}
        />

        <label htmlFor="register-name">顯示名稱</label>
        <input
          id="register-name"
          type="text"
          maxLength={80}
          required
          value={displayName}
          onChange={(e) => setDisplayName(e.target.value)}
        />

        <label htmlFor="register-password">密碼（至少 {passwordMinLength} 個字元）</label>
        <input
          id="register-password"
          type="password"
          autoComplete="new-password"
          minLength={passwordMinLength}
          required
          value={password}
          onChange={(e) => setPassword(e.target.value)}
        />

        {error && (
          <p className="auth-form__error" role="alert">
            {error}
          </p>
        )}

        <button className="cta" type="submit" disabled={isSubmitting}>
          {isSubmitting ? '註冊中…' : '註冊'}
        </button>
      </form>

      <GoogleSignInButton
        onSuccess={() => navigate(returnPath, { replace: true })}
        onFailure={setError}
      />

      <p className="auth__switch">
        已經有帳號了？<Link to="/login" state={location.state}>去登入</Link>
      </p>
    </section>
  );
}
