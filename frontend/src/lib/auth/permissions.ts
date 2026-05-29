import { useAppSelector } from '@/lib/store/store';

/** Permission codes — mirror of backend Permissions.cs / V005__auth.sql. */
export const P = {
  CustomerRead: 'customer.read',
  CustomerWrite: 'customer.write',
  VehicleRead: 'vehicle.read',
  VehicleWrite: 'vehicle.write',
  LookupRead: 'lookup.read',
  LookupManage: 'lookup.manage',
  QuotationRead: 'quotation.read',
  QuotationWrite: 'quotation.write',
  PolicyRead: 'policy.read',
  PolicyIssue: 'policy.issue',
  PolicyActivate: 'policy.activate',
  PolicyCancel: 'policy.cancel',
  PolicyRenew: 'policy.renew',
  ClaimRead: 'claim.read',
  ClaimFile: 'claim.file',
  ClaimReview: 'claim.review',
  ClaimApprove: 'claim.approve',
  ClaimReject: 'claim.reject',
  PaymentRead: 'payment.read',
  PaymentSettle: 'payment.settle',
  DashboardRead: 'dashboard.read',
} as const;

/** Minimum permission required to *view* each page. Mutations are gated server-side. */
export const ROUTE_PERMISSION: Record<string, string> = {
  '/': P.DashboardRead,
  '/customers': P.CustomerRead,
  '/vehicles': P.VehicleRead,
  '/quotations': P.QuotationRead,
  '/policies': P.PolicyRead,
  '/claims': P.ClaimRead,
  '/payments': P.PaymentRead,
  '/master': P.LookupManage,
};

/** Resolve the permission guarding a given pathname (longest matching prefix). */
export function requiredPermission(pathname: string): string | undefined {
  if (pathname === '/') return ROUTE_PERMISSION['/'];
  const match = Object.entries(ROUTE_PERMISSION)
    .filter(([path]) => path !== '/' && pathname.startsWith(path))
    .sort((a, b) => b[0].length - a[0].length)[0];
  return match?.[1];
}

export function usePermissions(): string[] {
  return useAppSelector((s) => s.auth.user?.permissions ?? []);
}

/** Returns true when no permission is required or the user holds it. */
export function useHasPermission(permission?: string): boolean {
  const permissions = usePermissions();
  return !permission || permissions.includes(permission);
}
