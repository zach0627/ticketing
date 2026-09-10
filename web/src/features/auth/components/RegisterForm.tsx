import type { FormEvent } from 'react';

/** 與後端 RegisterRequest 的 DataAnnotations 對齊；真正的把關仍在後端。 */
const passwordMinLength = 8;

export function RegisterForm({
  email,
  setEmail,
  password,
  setPassword,
  error,
  isSubmitting,
  handleSubmit,
  displayName,
  setDisplayName,
}: {
  email: string;
  setEmail: (value: string) => void;
  password: string;
  setPassword: (value: string) => void;
  error: string | null;
  isSubmitting: boolean;
  handleSubmit: (event: FormEvent) => Promise<void>;
  displayName: string;
  setDisplayName: (value: string) => void;
}) {
  return (
    <form className="auth-form" onSubmit={handleSubmit}>
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

      <label htmlFor="register-password">
        密碼（至少 {passwordMinLength} 個字元）
      </label>
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
  );
}
