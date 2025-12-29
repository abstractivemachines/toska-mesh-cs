import { Panel } from '../components/common';
import { Link } from '../router';

export function NotFoundPage() {
  return (
    <Panel>
      <h2>Page Not Found</h2>
      <p>The page you're looking for doesn't exist.</p>
      <Link to="/">Go to Services</Link>
    </Panel>
  );
}
