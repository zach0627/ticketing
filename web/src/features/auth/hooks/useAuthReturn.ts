import { useLocation, useNavigate } from 'react-router';
import { safeReturnPath } from '../model/returnPath';

/** Email 與 Google 登入共用安全返回路徑，保留原選位草稿。 */
export function useAuthReturn() {
  const navigate = useNavigate();
  const location = useLocation();
  const returnPath = safeReturnPath(
    (location.state as { from?: unknown } | null)?.from,
  );
  const complete = () =>
    navigate(returnPath, {
      replace: true,
      state: {
        seatDraft: (location.state as { seatDraft?: unknown } | null)
          ?.seatDraft,
      },
    });
  return { returnState: location.state, complete };
}
