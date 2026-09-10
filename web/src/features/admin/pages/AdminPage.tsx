import '../styles/admin.css';
import { ErrorMessage, Spinner } from '../../../shared/ui/States';
import { formatTaipei } from '../../../shared/utils/format';
import { AdminOrders } from '../components/AdminOrders';
import { DashboardStats } from '../components/DashboardStats';
import { PerformanceTable } from '../components/PerformanceTable';
import { ResetPanel } from '../components/ResetPanel';
import { useAdminDashboard } from '../hooks/useAdminDashboard';

export function AdminPage() {
  const {
    dashboard,
    orders,
    performanceId,
    setPerformanceId,
    confirmation,
    setConfirmation,
    message,
    error,
    togglePause,
    reset,
    busy,
  } = useAdminDashboard();
  if (dashboard.isPending) return <Spinner />;
  if (dashboard.error) return <ErrorMessage error={dashboard.error} />;

  return (
    <section className="admin-page">
      <h1>管理後台</h1>
      <p className="page-header__note">
        伺服器時間 {formatTaipei(dashboard.data.serverNowUtc)}
      </p>

      {message && (
        <p className="notice" role="status">
          {message}
        </p>
      )}
      {error && (
        <p className="auth-form__error" role="alert">
          {error}
        </p>
      )}

      <DashboardStats data={dashboard.data} />

      <PerformanceTable
        performances={dashboard.data.performances}
        busy={busy}
        onTogglePause={togglePause.mutate}
        onSelectPerformance={setPerformanceId}
      />

      <AdminOrders
        performanceId={performanceId}
        onClearFilter={() => setPerformanceId(null)}
        orders={orders.data}
        isPending={orders.isPending}
        error={orders.error}
      />

      <ResetPanel
        confirmation={confirmation}
        onConfirmationChange={setConfirmation}
        busy={busy}
        isResetting={reset.isPending}
        onReset={() => reset.mutate()}
      />
    </section>
  );
}
