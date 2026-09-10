import { Link } from 'react-router';
import { AuthShell } from '../components/AuthShell';
import { GoogleSignInButton } from '../components/GoogleSignInButton';
import { LoginForm } from '../components/LoginForm';
import { useLogin } from '../hooks/useLogin';

export function LoginPage() {
  const {
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
  } = useLogin();
  return (
    <AuthShell title="歡迎回來" subtitle="登入帳號，繼續你的下一場精彩。">
      <LoginForm
        email={email}
        setEmail={setEmail}
        password={password}
        setPassword={setPassword}
        error={error}
        isSubmitting={isSubmitting}
        handleSubmit={handleSubmit}
      />

      <GoogleSignInButton onSuccess={complete} onFailure={setError} />

      <p className="auth__switch">
        還沒有帳號？
        <Link to="/register" state={returnState}>
          立即註冊
        </Link>
      </p>
    </AuthShell>
  );
}
