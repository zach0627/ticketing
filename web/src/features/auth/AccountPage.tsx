import { Link } from 'react-router';
import { useAuth } from './authContext';

/**
 * 「我的帳號」。內容很少，但它證明了整條線是通的：
 * 登入拿到 token → RequireAuth 放行 → 帶 Bearer 打 `/me` → 後端從 JWT 的 sub 找到人。
 *
 * 訂單與保留在階段 6／7 加進來。
 */
export function AccountPage() {
  const { user, signOut } = useAuth();
  if (!user) return null;   // RequireAuth 已經擋過，這行只是讓型別收斂

  return (
    <section className="account">
      <Link className="back-link" to="/">← 回到活動列表</Link>

      <h1>我的帳號</h1>

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

      <button className="cta cta--secondary" type="button" onClick={signOut}>
        登出
      </button>
    </section>
  );
}
