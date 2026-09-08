import { Link, useLocation, useNavigate } from 'react-router';
import { useAuth } from '../features/auth/authContext';

/**
 * 每一頁都有的頁首。登入狀態放這裡，使用者才知道自己是誰、能不能買票。
 */
export function SiteHeader() {
  const { user, isRestoring, signOut } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();

  return (
    <header className="site-header">
      <Link className="site-header__brand" to="/">
        線上購票系統
      </Link>

      <nav className="site-header__nav" aria-label="帳號">
        {isRestoring ? (
          <span className="site-header__hint">確認登入狀態…</span>
        ) : user ? (
          <>
            {user.role === 'Admin' && <Link to="/admin">後台</Link>}
            <Link to="/orders">我的訂單</Link>
            <Link to="/account">{user.displayName}</Link>
            <button
              className="site-header__link-button"
              type="button"
              onClick={() => {
                signOut();
                navigate('/', { replace: true });
              }}
            >
              登出
            </button>
          </>
        ) : (
          // 記住現在在哪一頁，登入完再回來
          <Link to="/login" state={{ from: `${location.pathname}${location.search}` }}>
            登入
          </Link>
        )}
      </nav>
    </header>
  );
}
