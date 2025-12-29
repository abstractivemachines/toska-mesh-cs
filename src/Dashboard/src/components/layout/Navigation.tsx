import { Link } from '../../router';

interface NavItem {
  path: string;
  label: string;
}

const NAV_ITEMS: NavItem[] = [
  { path: '/', label: 'Services' },
  { path: '/traces', label: 'Traces' },
];

export function Navigation() {
  return (
    <nav className="nav-tabs">
      {NAV_ITEMS.map((item) => (
        <Link
          key={item.path}
          to={item.path}
          className="nav-tab"
          activeClassName="nav-tab-active"
        >
          {item.label}
        </Link>
      ))}
    </nav>
  );
}
