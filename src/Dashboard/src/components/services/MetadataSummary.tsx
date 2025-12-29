import type { DashboardServiceMetadataSummary } from '../../types/api';

interface MetadataSummaryProps {
  metadata: DashboardServiceMetadataSummary | null;
}

function isValidDate(dateStr: string): boolean {
  if (!dateStr) return false;
  const date = new Date(dateStr);
  if (isNaN(date.getTime())) return false;
  if (date.getFullYear() <= 1) return false;
  return true;
}

function formatDate(dateStr: string): string {
  if (!isValidDate(dateStr)) {
    return '-';
  }
  try {
    const date = new Date(dateStr);
    return date.toLocaleString();
  } catch {
    return '-';
  }
}

export function MetadataSummary({ metadata }: MetadataSummaryProps) {
  if (!metadata) {
    return <p className="muted">No metadata available.</p>;
  }

  return (
    <div className="metadata-summary">
      <div className="metadata-header">
        <span>{metadata.instanceCount} instance(s)</span>
        <span className="muted">Generated: {formatDate(metadata.generatedAt)}</span>
      </div>

      {metadata.keys.length === 0 ? (
        <p className="muted">No metadata keys defined.</p>
      ) : (
        <div className="metadata-keys">
          {metadata.keys.map((keySummary) => (
            <div key={keySummary.key} className="metadata-key-card">
              <div className="metadata-key-header">
                <strong>{keySummary.key}</strong>
                <span className="muted">
                  {keySummary.instanceCount} of {metadata.instanceCount} instances
                </span>
              </div>
              <div className="metadata-key-values">
                {keySummary.values.map((value) => (
                  <span key={value} className="pill pill-small">
                    {value}
                  </span>
                ))}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
