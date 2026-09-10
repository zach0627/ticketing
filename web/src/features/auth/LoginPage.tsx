import { useState } from 'react';
import { AuthShell } from './AuthShell';
import type { FormEvent } from 'react';
import { Link, useLocation, useNavigate } from 'react-router';
import { ApiError } from '../../shared/api/http';
import { authApi } from './api';
import { useAuth } from './authContext';
import { GoogleSignInButton } from './GoogleSignInButton';
import { safeReturnPath } from './returnPath';

export function LoginPage() {
  const { signIn } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  const returnPath = safeReturnPath(
    (location.state as { from?: unknown } | null)?.from,
  );

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
      navigate(returnPath, {
        replace: true,
        state: {
          seatDraft: (location.state as { seatDraft?: unknown } | null)
            ?.seatDraft,
        },
      });
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

  return (
    <AuthShell title="歡迎回來" subtitle="登入帳號，繼續你的下一場精彩。">
      <form className="auth-form" onSubmit={handleSubmit}>
        <label htmlFor="login-email">Email</label>
        <input
          id="login-email"
          type="email"
          autoComplete="email"
          required
          value={email}
          onChange={(e) => setEmail(e.target.value)}
        />

        <label htmlFor="login-password">密碼</label>
        <input
          id="login-password"
          type="password"
          autoComplete="current-password"
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
          {isSubmitting ? '登入中…' : '登入'}
        </button>
      </form>

      <GoogleSignInButton
        onSuccess={() =>
          navigate(returnPath, {
            replace: true,
            state: {
              seatDraft: (location.state as { seatDraft?: unknown } | null)
                ?.seatDraft,
            },
          })
        }
        onFailure={setError}
      />

      <p className="auth__switch">
        還沒有帳號？
        <Link to="/register" state={location.state}>
          立即註冊
        </Link>
      </p>
    </AuthShell>
  );
}
