import { Link } from 'react-router';
import { AuthShell } from '../components/AuthShell';
import { GoogleSignInButton } from '../components/GoogleSignInButton';
import { RegisterForm } from '../components/RegisterForm';
import { useRegister } from '../hooks/useRegister';

export function RegisterPage() {
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
    displayName,
    setDisplayName,
  } = useRegister();
  return (
    <AuthShell
      title="註冊會員"
      subtitle="建立帳號，讓喜歡的活動成為生活的一部分。"
    >
      <RegisterForm
        email={email}
        setEmail={setEmail}
        password={password}
        setPassword={setPassword}
        error={error}
        isSubmitting={isSubmitting}
        handleSubmit={handleSubmit}
        displayName={displayName}
        setDisplayName={setDisplayName}
      />

      <GoogleSignInButton onSuccess={complete} onFailure={setError} />

      <p className="auth__switch">
        已經有帳號了？
        <Link to="/login" state={returnState}>
          立即登入
        </Link>
      </p>
    </AuthShell>
  );
}
