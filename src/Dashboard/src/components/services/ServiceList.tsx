import { useServices } from '../../hooks';
import { LoadingState, ErrorState, Panel } from '../common';
import { ServiceCard } from './ServiceCard';

export function ServiceList() {
  const { services, loading, error, refetch } = useServices();

  return (
    <Panel title="Service Catalog">
      {loading && <LoadingState message="Loading services from the gateway..." />}
      {!loading && error && <ErrorState message={`Unable to load services: ${error}`} onRetry={refetch} />}
      {!loading && !error && services.length === 0 && (
        <p>No services discovered yet.</p>
      )}
      {!loading && !error && services.length > 0 && (
        <ul className="service-list">
          {services.map((service) => (
            <ServiceCard key={service.serviceName} service={service} />
          ))}
        </ul>
      )}
    </Panel>
  );
}
