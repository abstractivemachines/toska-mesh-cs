import type { DashboardServiceCatalogItem, HealthStatus } from '../../types/api';
import { Panel } from '../common';
import { InstanceTable } from './InstanceTable';
import { HealthHistory } from './HealthHistory';
import { MetadataSummary } from './MetadataSummary';
import { Link } from '../../router';

interface ServiceDetailProps {
  service: DashboardServiceCatalogItem;
}

function getOverallStatus(service: DashboardServiceCatalogItem): HealthStatus {
  if (service.health.length === 0) return 'Unknown';

  const hasUnhealthy = service.health.some((h) => h.status === 'Unhealthy');
  if (hasUnhealthy) return 'Unhealthy';

  const hasDegraded = service.health.some((h) => h.status === 'Degraded');
  if (hasDegraded) return 'Degraded';

  const allHealthy = service.health.every((h) => h.status === 'Healthy');
  if (allHealthy) return 'Healthy';

  return 'Unknown';
}

export function ServiceDetail({ service }: ServiceDetailProps) {
  const status = getOverallStatus(service);

  return (
    <div className="service-detail">
      <div className="service-detail-header">
        <Link to="/" className="back-link">&larr; Back to Services</Link>
        <div className="service-title-row">
          <h2>{service.serviceName}</h2>
          <span className={`status-indicator status-${status.toLowerCase()}`}>{status}</span>
        </div>
      </div>

      <Panel title="Instances" className="detail-panel">
        <InstanceTable instances={service.instances} />
      </Panel>

      <Panel title="Health History" className="detail-panel">
        <HealthHistory snapshots={service.health} />
      </Panel>

      <Panel title="Metadata Summary" className="detail-panel">
        <MetadataSummary metadata={service.metadata} />
      </Panel>
    </div>
  );
}
