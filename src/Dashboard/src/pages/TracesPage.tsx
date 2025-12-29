import { Panel } from '../components/common';
import { TraceList } from '../components/traces';

export function TracesPage() {
  return (
    <Panel title="Distributed Traces">
      <TraceList />
    </Panel>
  );
}
