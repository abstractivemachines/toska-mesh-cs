import { useServiceDetail } from '../hooks';
import { ServiceDetail } from '../components/services';
import { MetricsPanel } from '../components/metrics';
import { LoadingState, ErrorState } from '../components/common';

interface ServicePageProps {
  serviceName: string;
}

export function ServicePage({ serviceName }: ServicePageProps) {
  const decodedName = decodeURIComponent(serviceName);
  const { service, loading, error, refetch } = useServiceDetail(decodedName);

  if (loading) {
    return <LoadingState message={`Loading ${decodedName}...`} />;
  }

  if (error) {
    return <ErrorState message={error} onRetry={refetch} />;
  }

  if (!service) {
    return <ErrorState message={`Service "${decodedName}" not found`} />;
  }

  return (
    <>
      <ServiceDetail service={service} />
      <MetricsPanel serviceName={decodedName} />
    </>
  );
}
