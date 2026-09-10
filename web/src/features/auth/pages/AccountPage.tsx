import '../styles/account.css';
import { Link } from 'react-router';
import { Icon } from '../../../shared/ui/Icon';
import { useAuth } from '../context/authContext';

/**
 * 「我的帳號」。內容很少，但它證明了整條線是通的：
 * 登入拿到 token → RequireAuth 放行 → 帶 Bearer 打 `/me` → 後端從 JWT 的 sub 找到人。
 *
 * 訂單與保留在階段 6／7 加進來。
 */
export function AccountPage() {
  const { user, signOut } = useAuth();
  if (!user) return null; // RequireAuth 已經擋過，這行只是讓型別收斂

  return (
    <section className="account">
      <Link className="back-link" to="/">
        ← 回到活動列表
      </Link>

      <header className="page-heading">
        <h1>我的帳號</h1>
        <p>你好，{user.displayName}。下一場想去哪裡？</p>
      </header>

      <dl className="detail__facts">
        <div>
          <dt>顯示名稱</dt>
          <dd>{user.displayName}</dd>
        </div>
        <div>
          <dt>Email</dt>
          <dd>{user.email}</dd>
        </div>
        <div>
          <dt>角色</dt>
          <dd>{user.role === 'Admin' ? '管理者' : '一般會員'}</dd>
        </div>
      </dl>

      <div className="account-actions">
        <Link to="/orders">
          <Icon name="ticket" size={21} />
          <span>
            <strong>我的訂單與票券</strong>
            <small>查看已完成的訂單與座位明細</small>
          </span>
          <Icon name="chevron" size={17} />
        </Link>
        {user.role === 'Admin' && (
          <Link to="/admin">
            <Icon name="grid" size={21} />
            <span>
              <strong>管理後台</strong>
              <small>管理售票狀態與檢視訂單</small>
            </span>
            <Icon name="chevron" size={17} />
          </Link>
        )}
      </div>
      <button className="cta cta--secondary" type="button" onClick={signOut}>
        登出
      </button>
    </section>
  );
}
