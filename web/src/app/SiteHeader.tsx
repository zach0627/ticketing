import { useState } from 'react';
import { Link, NavLink, useLocation, useNavigate } from 'react-router';
import { useAuth } from '../features/auth/authContext';
import { Icon } from '../shared/ui/Icon';

export function SiteHeader() {
  const { user, isRestoring, signOut } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [menuOpen, setMenuOpen] = useState(false);

  return (
    <header className="site-header">
      <div className="container site-header__inner">
        <Link
          className="brand"
          to="/"
          onClick={() => setMenuOpen(false)}
          aria-label="線上購票系統首頁"
        >
          <Icon name="ticket" size={30} />
          <span>
            線上購票系統<small>ticketing</small>
          </span>
        </Link>
        <nav className="site-header__primary" aria-label="主要導覽">
          <NavLink to="/" end>
            探索活動
          </NavLink>
          <NavLink to="/guide">購票指南</NavLink>
        </nav>
        <div className="site-header__account">
          {isRestoring ? (
            <span className="muted">確認登入狀態…</span>
          ) : user ? (
            <>
              {user.role === 'Admin' && (
                <Link className="header-admin" to="/admin">
                  管理後台
                </Link>
              )}
              <Link className="header-orders" to="/orders">
                <Icon name="ticket" size={18} />
                我的訂單
              </Link>
              <Link
                className="account-link"
                to="/account"
                aria-label="我的帳號"
              >
                <span className="avatar">{user.displayName.slice(0, 1)}</span>
                <span className="account-name">{user.displayName}</span>
              </Link>
            </>
          ) : (
            <Link
              className="header-login"
              to="/login"
              state={{ from: `${location.pathname}${location.search}` }}
            >
              <Icon name="user" size={17} />
              <span>登入 / 註冊</span>
            </Link>
          )}
          <button
            type="button"
            className="icon-button menu-button"
            aria-label={menuOpen ? '關閉選單' : '開啟選單'}
            aria-expanded={menuOpen}
            aria-controls="mobile-nav"
            onClick={() => setMenuOpen(!menuOpen)}
          >
            <Icon name={menuOpen ? 'close' : 'menu'} />
          </button>
        </div>
      </div>
      {menuOpen && (
        <nav
          id="mobile-nav"
          className="mobile-nav"
          aria-label="行動版導覽"
          onKeyDown={(event) => {
            if (event.key === 'Escape') setMenuOpen(false);
          }}
        >
          <Link to="/" onClick={() => setMenuOpen(false)}>
            探索活動
          </Link>
          <Link to="/guide" onClick={() => setMenuOpen(false)}>
            購票指南
          </Link>
          <Link to="/orders" onClick={() => setMenuOpen(false)}>
            我的訂單
          </Link>
          {user?.role === 'Admin' && (
            <Link to="/admin" onClick={() => setMenuOpen(false)}>
              管理後台
            </Link>
          )}
          {user && (
            <button
              type="button"
              onClick={() => {
                signOut();
                setMenuOpen(false);
                navigate('/');
              }}
            >
              登出
            </button>
          )}
        </nav>
      )}
    </header>
  );
}
