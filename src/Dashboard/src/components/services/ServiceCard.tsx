import type { DashboardServiceCatalogItem, HealthStatus } from '../../types/api';
import { StatusBadge } from '../common';
import { navigate } from '../../router';

interface ServiceCardProps {
  service: DashboardServiceCatalogItem;
}

// Backend returns numeric enum values, convert to string
// 0 = Unknown, 1 = Healthy, 2 = Unhealthy, 3 = Degraded
function normalizeStatus(status: HealthStatus | number): HealthStatus {
  if (typeof status === 'number') {
    switch (status) {
      case 1: return 'Healthy';
      case 2: return 'Unhealthy';
      case 3: return 'Degraded';
      default: return 'Unknown';
    }
  }
  return status;
}

function getOverallStatus(service: DashboardServiceCatalogItem): HealthStatus {
  if (service.health.length === 0) return 'Unknown';

  const statuses = service.health.map((h) => normalizeStatus(h.status));

  const hasUnhealthy = statuses.some((s) => s === 'Unhealthy');
  if (hasUnhealthy) return 'Unhealthy';

  const hasDegraded = statuses.some((s) => s === 'Degraded');
  if (hasDegraded) return 'Degraded';

  const allHealthy = statuses.every((s) => s === 'Healthy');
  if (allHealthy) return 'Healthy';

  return 'Unknown';
}

export function ServiceCard({ service }: ServiceCardProps) {
  const status = getOverallStatus(service);

  const handleClick = () => {
    navigate(`/services/${encodeURIComponent(service.serviceName)}`);
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault();
      handleClick();
    }
  };

  return (
    <li
      className="service-card"
      onClick={handleClick}
      onKeyDown={handleKeyDown}
      tabIndex={0}
      role="button"
      aria-label={`View details for ${service.serviceName}`}
    >
      <div className="service-row">
        <div>
          <h3>{service.serviceName}</h3>
          <p>
            {service.instances.length} instance(s) &bull;{' '}
            {service.health.length} health snapshot(s)
          </p>
        </div>
        <StatusBadge status={status} />
      </div>
    </li>
  );
}
