import { lazy, Suspense, type ComponentType } from 'react';
import { createBrowserRouter } from 'react-router';
import { Spinner } from '@/components/ui';
import { SiteLayout } from '@/site/SiteLayout';
import { HomePage } from '@/site/HomePage';
import { NotFoundPage } from '@/site/NotFoundPage';
import { RouteError } from '@/components/RouteError';

// Кабинет, CRM и 3D грузятся отдельными частями, чтобы главная открывалась быстро.
function page(load: () => Promise<{ default: ComponentType }>) {
  const Component = lazy(load);
  return (
    <Suspense
      fallback={
        <div className="flex min-h-[50vh] items-center justify-center text-muted">
          <Spinner />
        </div>
      }
    >
      <Component />
    </Suspense>
  );
}

export const router = createBrowserRouter([
  {
    element: <SiteLayout />,
    errorElement: <RouteError />,
    children: [
      { path: '/', element: <HomePage /> },
      { path: '/building/:id', element: page(() => import('@/site/BuildingPage')) },
      { path: '/floors/:id', element: page(() => import('@/site/FloorPage')) },
      { path: '/apartments/:id', element: page(() => import('@/site/ApartmentPage')) },
      { path: '/apartments', element: page(() => import('@/site/ApartmentsPage')) },
      { path: '/contacts', element: page(() => import('@/site/ContactsPage')) },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
  {
    path: '/cabinet/login',
    errorElement: <RouteError />,
    element: page(() => import('@/cabinet/CabinetLogin')),
  },
  {
    path: '/cabinet',
    errorElement: <RouteError />,
    element: page(() => import('@/cabinet/CabinetLayout')),
    children: [
      { index: true, element: page(() => import('@/cabinet/CabinetHome')) },
      { path: 'deals/:id', element: page(() => import('@/cabinet/CabinetDeal')) },
    ],
  },
  {
    path: '/crm/login',
    errorElement: <RouteError />,
    element: page(() => import('@/crm/CrmLogin')),
  },
  {
    path: '/crm',
    errorElement: <RouteError />,
    element: page(() => import('@/crm/CrmLayout')),
    children: [
      { index: true, element: page(() => import('@/crm/pages/Dashboard')) },
      { path: 'pipeline', element: page(() => import('@/crm/pages/Pipeline')) },
      { path: 'leads', element: page(() => import('@/crm/pages/Leads')) },
      { path: 'customers', element: page(() => import('@/crm/pages/Customers')) },
      { path: 'customers/:id', element: page(() => import('@/crm/pages/CustomerDetail')) },
      { path: 'deals', element: page(() => import('@/crm/pages/Deals')) },
      { path: 'deals/:id', element: page(() => import('@/crm/pages/DealDetail')) },
      { path: 'apartments', element: page(() => import('@/crm/pages/Chessboard')) },
      { path: 'rates', element: page(() => import('@/crm/pages/Rates')) },
      { path: 'reports', element: page(() => import('@/crm/pages/Reports')) },
      { path: 'catalog', element: page(() => import('@/crm/pages/Catalog')) },
      { path: 'construction', element: page(() => import('@/crm/pages/Construction')) },
      { path: 'audit', element: page(() => import('@/crm/pages/Audit')) },
      { path: 'users', element: page(() => import('@/crm/pages/Users')) },
      { path: '*', element: page(() => import('@/crm/pages/CrmNotFound')) },
    ],
  },
]);
