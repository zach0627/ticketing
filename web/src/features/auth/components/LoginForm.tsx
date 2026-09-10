import type { FormEvent } from 'react';

export function LoginForm({
  email,
  setEmail,
  password,
  setPassword,
  error,
  isSubmitting,
  handleSubmit,
}: {
  email: string;
  setEmail: (value: string) => void;
  password: string;
  setPassword: (value: string) => void;
  error: string | null;
  isSubmitting: boolean;
  handleSubmit: (event: FormEvent) => Promise<void>;
}) {
  return (
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
  );
}
