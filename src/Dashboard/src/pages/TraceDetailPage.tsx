import { TraceDetail } from '../components/traces';

interface TraceDetailPageProps {
  traceId: string;
}

export function TraceDetailPage({ traceId }: TraceDetailPageProps) {
  const decodedId = decodeURIComponent(traceId);
  return <TraceDetail traceId={decodedId} />;
}
