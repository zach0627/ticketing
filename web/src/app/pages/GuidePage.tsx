import '../styles/guide.css';
import { Link } from 'react-router';
import { Icon } from '../../shared/ui/Icon';

export function GuidePage() {
  return (
    <article className="guide-page">
      <header className="page-heading">
        <h1>從喜歡，到在現場。</h1>
        <p>找到活動、選好座位，跟著三個步驟完成購票。</p>
      </header>
      <div className="guide-grid">
        {[
          [
            'search',
            '01',
            '找到你的下一場',
            '依活動分類、城市或關鍵字探索，先查看演出時間、場館與票價。',
          ],
          [
            'ticket',
            '02',
            '選一個喜歡的位子',
            '選擇票區並點選座位；運動賽事也可選擇自動連號。登入後即可保留座位。',
          ],
          [
            'check',
            '03',
            '確認，收好你的票',
            '座位保留五分鐘。請在期限內確認明細並完成模擬付款，票券會存放在我的訂單。',
          ],
        ].map(([icon, number, title, text]) => (
          <section key={number}>
            <Icon name={icon as 'search' | 'ticket' | 'check'} size={28} />
            <h2>{title}</h2>
            <p>{text}</p>
          </section>
        ))}
      </div>
      <section className="faq">
        <h2>購票前，先了解這些事</h2>
        <details open>
          <summary>這裡的票券可以入場嗎？</summary>
          <p>
            本站活動、演出者與場館皆為虛構，付款為模擬流程，不會扣取任何款項。
            <strong>票券無實際入場效力。</strong>
          </p>
        </details>
        <details>
          <summary>選好座位就代表買到了嗎？</summary>
          <p>
            點選座位尚未完成保留。按下「確認保留」並成功進入付款頁後，座位才會為你保留五分鐘；完成模擬付款後可取得票券。
          </p>
        </details>
        <details>
          <summary>一次可以購買幾張票？</summary>
          <p>
            每場活動的購票上限會顯示在活動及選位頁。已付款票券也計入上限，同一場次一次只能有一筆有效保留；每筆保留選擇同一票區。
          </p>
        </details>
        <details>
          <summary>付款失敗或保留到期怎麼辦？</summary>
          <p>
            付款失敗時，只要座位仍在保留期間，就可以重新嘗試。到期或取消後，請回到座位圖重新選位。
          </p>
        </details>
        <details>
          <summary>第一次載入比較久怎麼辦？</summary>
          <p>
            服務閒置後可能需要約一分鐘恢復。請稍候；若畫面顯示連線失敗，可以按下重新載入。
          </p>
        </details>
      </section>
      <Link to="/" className="cta">
        開始探索活動
        <Icon name="arrow" size={18} />
      </Link>
    </article>
  );
}
