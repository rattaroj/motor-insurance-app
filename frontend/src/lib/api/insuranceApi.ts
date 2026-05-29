import {
  createApi,
  fetchBaseQuery,
  type BaseQueryFn,
  type FetchArgs,
  type FetchBaseQueryError,
} from '@reduxjs/toolkit/query/react';
import { setCredentials, clearCredentials, type AuthResponse, type UserProfile } from '../auth/authSlice';
import type { RootState } from '../store/store';

export type PolicyStatus = 'Draft' | 'Quoted' | 'Issued' | 'Active' | 'Cancelled' | 'Expired';
export type ClaimStatus = 'Filed' | 'UnderReview' | 'Assessment' | 'Approved' | 'Rejected' | 'Paid' | 'Closed';
export type CoverageType = 'Type1' | 'Type2Plus' | 'Type3Plus' | 'Type3';

export interface CustomerDto {
  id: number;
  nationalId: string;
  fullName: string;
  phone: string | null;
  email: string | null;
}

/** Submodel fuel type — mirrors backend Powertrain enum (serialized as its C# name). */
export type Powertrain = 'Gasoline' | 'Diesel' | 'Electric' | 'Hybrid';

export const POWERTRAIN_LABELS: Record<Powertrain, string> = {
  Gasoline: 'น้ำมัน',
  Diesel: 'ดีเซล',
  Electric: 'ไฟฟ้า',
  Hybrid: 'ไฮบริด',
};

export const POWERTRAIN_OPTIONS = (Object.keys(POWERTRAIN_LABELS) as Powertrain[]).map((v) => ({
  value: v,
  label: POWERTRAIN_LABELS[v],
}));

export interface VehicleDto {
  id: number;
  customerId: number;
  customerName: string;
  registrationNo: string;
  province: string;
  modelYearId: number;
  brand: string;
  model: string;
  submodel: string;
  powertrain: Powertrain;
  year: number;
  chassisNo: string | null;
}

export interface Option {
  id: number;
  name: string;
}

export interface SubmodelOption {
  id: number;
  name: string;
  powertrain: Powertrain;
}

export interface ModelYearOption {
  id: number;
  year: number;
}

export interface QuotationDto {
  id: number;
  quotationNo: string;
  customerId: number;
  customerName: string;
  vehicleId: number;
  vehicleRegistration: string;
  coverageType: string;
  sumInsured: number;
  premium: number;
  validUntil: string;
}

export interface PolicyDto {
  id: number;
  policyNo: string;
  customerId: number;
  customerName: string;
  vehicleId: number;
  vehicleRegistration: string;
  status: PolicyStatus;
  coverageType: string;
  sumInsured: number;
  premium: number;
  effectiveDate: string | null;
  expiryDate: string | null;
  previousPolicyId: number | null;
}

export interface PolicyHistoryDto {
  status: string;
  premium: number;
  validFrom: string;
  validTo: string;
}

export interface ClaimDto {
  id: number;
  claimNo: string;
  policyId: number;
  policyNo: string;
  status: ClaimStatus;
  incidentDate: string;
  description: string | null;
  claimedAmount: number;
  approvedAmount: number | null;
  rejectReason: string | null;
}

export interface PaymentDto {
  id: number;
  paymentNo: string;
  direction: string;
  status: string;
  policyId: number | null;
  policyNo: string | null;
  claimId: number | null;
  claimNo: string | null;
  amount: number;
  paidAt: string | null;
  referenceNo: string | null;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasNext: boolean;
  hasPrevious: boolean;
}

export interface DashboardSummary {
  customers: number;
  vehicles: number;
  quotations: number;
  policiesTotal: number;
  policiesActive: number;
  claimsOpen: number;
  paymentsPending: number;
  paymentsPendingAmount: number;
}

const baseUrl = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000/api';

/** Build a query string, skipping undefined/null/empty values. */
const qs = (params: Record<string, string | number | undefined | null>) => {
  const sp = new URLSearchParams();
  for (const [k, v] of Object.entries(params)) {
    if (v !== undefined && v !== null && v !== '') sp.set(k, String(v));
  }
  return sp.toString();
};

const rawBaseQuery = fetchBaseQuery({
  baseUrl,
  credentials: 'include', // send/receive the httpOnly refresh cookie
  prepareHeaders: (headers, { getState }) => {
    const token = (getState() as RootState).auth.accessToken;
    if (token) headers.set('Authorization', `Bearer ${token}`);
    return headers;
  },
});

/** Unwraps the global ApiResponse envelope so endpoints see `data` directly. */
const unwrapEnvelope: BaseQueryFn<string | FetchArgs, unknown, FetchBaseQueryError> = async (args, store, extra) => {
  const result = await rawBaseQuery(args, store, extra);
  if (result.data && typeof result.data === 'object' && 'success' in result.data) {
    return { ...result, data: (result.data as unknown as { data: unknown }).data };
  }
  return result;
};

const isAuthEndpoint = (args: string | FetchArgs) => {
  const url = typeof args === 'string' ? args : args.url;
  return url.startsWith('auth/');
};

// De-dupes concurrent refreshes: the first 401 kicks off /auth/refresh, the rest await it.
let refreshPromise: ReturnType<typeof unwrapEnvelope> | null = null;

/** On 401, silently refresh the access token (from the cookie) once, then retry. */
const baseQuery: BaseQueryFn<string | FetchArgs, unknown, FetchBaseQueryError> = async (args, store, extra) => {
  let result = await unwrapEnvelope(args, store, extra);

  if (result.error?.status === 401 && !isAuthEndpoint(args)) {
    refreshPromise ??= unwrapEnvelope({ url: 'auth/refresh', method: 'POST' }, store, extra);
    const refresh = await refreshPromise;
    refreshPromise = null;

    if (refresh.data) {
      store.dispatch(setCredentials(refresh.data as AuthResponse));
      result = await unwrapEnvelope(args, store, extra);
    } else {
      store.dispatch(clearCredentials());
    }
  }

  return result;
};

export const insuranceApi = createApi({
  reducerPath: 'insuranceApi',
  baseQuery,
  tagTypes: [
    'Customer',
    'Vehicle',
    'Quotation',
    'Policy',
    'PolicyHistory',
    'Claim',
    'Payment',
    'VBrand',
    'VModel',
    'VSubmodel',
    'VYear',
  ],
  endpoints: (build) => ({
    // ---------- Auth ----------
    login: build.mutation<AuthResponse, { username: string; password: string }>({
      query: (body) => ({ url: 'auth/login', method: 'POST', body }),
    }),
    refresh: build.mutation<AuthResponse, void>({
      query: () => ({ url: 'auth/refresh', method: 'POST' }),
    }),
    logout: build.mutation<void, void>({
      query: () => ({ url: 'auth/logout', method: 'POST' }),
    }),
    getMe: build.query<UserProfile, void>({
      query: () => 'auth/me',
    }),

    // ---------- Customers ----------
    getCustomers: build.query<PagedResult<CustomerDto>, { page?: number; pageSize?: number; search?: string } | void>({
      query: (a) => `customers?${qs({ page: a?.page, pageSize: a?.pageSize, search: a?.search })}`,
      providesTags: ['Customer'],
    }),
    createCustomer: build.mutation<
      { id: number },
      { nationalId: string; fullName: string; phone?: string; email?: string }
    >({
      query: (body) => ({ url: 'customers', method: 'POST', body }),
      invalidatesTags: ['Customer'],
    }),

    // ---------- Vehicles ----------
    getVehicles: build.query<
      PagedResult<VehicleDto>,
      { page?: number; pageSize?: number; search?: string; customerId?: number } | void
    >({
      query: (a) =>
        `vehicles?${qs({ page: a?.page, pageSize: a?.pageSize, search: a?.search, customerId: a?.customerId })}`,
      providesTags: ['Vehicle'],
    }),
    createVehicle: build.mutation<
      { id: number },
      {
        customerId: number;
        registrationNo: string;
        province: string;
        modelYearId: number;
        chassisNo?: string;
      }
    >({
      query: (body) => ({ url: 'vehicles', method: 'POST', body }),
      invalidatesTags: ['Vehicle'],
    }),

    // ---------- Vehicle master-data lookups (cascading) ----------
    getVehicleBrands: build.query<Option[], void>({
      query: () => 'lookups/vehicle-brands',
      providesTags: ['VBrand'],
    }),
    getVehicleModels: build.query<Option[], number>({
      query: (brandId) => `lookups/vehicle-models?brandId=${brandId}`,
      providesTags: ['VModel'],
    }),
    getVehicleSubmodels: build.query<SubmodelOption[], number>({
      query: (modelId) => `lookups/vehicle-submodels?modelId=${modelId}`,
      providesTags: ['VSubmodel'],
    }),
    getVehicleModelYears: build.query<ModelYearOption[], number>({
      query: (submodelId) => `lookups/vehicle-model-years?submodelId=${submodelId}`,
      providesTags: ['VYear'],
    }),

    // ---------- Vehicle master-data CRUD ----------
    createBrand: build.mutation<{ id: number }, { name: string }>({
      query: (body) => ({ url: 'lookups/vehicle-brands', method: 'POST', body }),
      invalidatesTags: ['VBrand', 'Vehicle'],
    }),
    updateBrand: build.mutation<void, { id: number; name: string }>({
      query: ({ id, name }) => ({ url: `lookups/vehicle-brands/${id}`, method: 'PUT', body: { name } }),
      invalidatesTags: ['VBrand', 'Vehicle'],
    }),
    deleteBrand: build.mutation<void, number>({
      query: (id) => ({ url: `lookups/vehicle-brands/${id}`, method: 'DELETE' }),
      invalidatesTags: ['VBrand'],
    }),

    createModel: build.mutation<{ id: number }, { brandId: number; name: string }>({
      query: (body) => ({ url: 'lookups/vehicle-models', method: 'POST', body }),
      invalidatesTags: ['VModel', 'Vehicle'],
    }),
    updateModel: build.mutation<void, { id: number; name: string }>({
      query: ({ id, name }) => ({ url: `lookups/vehicle-models/${id}`, method: 'PUT', body: { name } }),
      invalidatesTags: ['VModel', 'Vehicle'],
    }),
    deleteModel: build.mutation<void, number>({
      query: (id) => ({ url: `lookups/vehicle-models/${id}`, method: 'DELETE' }),
      invalidatesTags: ['VModel'],
    }),

    createSubmodel: build.mutation<{ id: number }, { modelId: number; name: string; powertrain: Powertrain }>({
      query: (body) => ({ url: 'lookups/vehicle-submodels', method: 'POST', body }),
      invalidatesTags: ['VSubmodel', 'Vehicle'],
    }),
    updateSubmodel: build.mutation<void, { id: number; name: string; powertrain: Powertrain }>({
      query: ({ id, name, powertrain }) => ({
        url: `lookups/vehicle-submodels/${id}`,
        method: 'PUT',
        body: { name, powertrain },
      }),
      invalidatesTags: ['VSubmodel', 'Vehicle'],
    }),
    deleteSubmodel: build.mutation<void, number>({
      query: (id) => ({ url: `lookups/vehicle-submodels/${id}`, method: 'DELETE' }),
      invalidatesTags: ['VSubmodel'],
    }),

    createModelYear: build.mutation<{ id: number }, { submodelId: number; year: number }>({
      query: (body) => ({ url: 'lookups/vehicle-model-years', method: 'POST', body }),
      invalidatesTags: ['VYear', 'Vehicle'],
    }),
    updateModelYear: build.mutation<void, { id: number; year: number }>({
      query: ({ id, year }) => ({ url: `lookups/vehicle-model-years/${id}`, method: 'PUT', body: { year } }),
      invalidatesTags: ['VYear', 'Vehicle'],
    }),
    deleteModelYear: build.mutation<void, number>({
      query: (id) => ({ url: `lookups/vehicle-model-years/${id}`, method: 'DELETE' }),
      invalidatesTags: ['VYear'],
    }),

    // ---------- Quotations ----------
    getQuotations: build.query<PagedResult<QuotationDto>, { page?: number; pageSize?: number; search?: string } | void>({
      query: (a) => `quotations?${qs({ page: a?.page, pageSize: a?.pageSize, search: a?.search })}`,
      providesTags: ['Quotation'],
    }),
    createQuotation: build.mutation<
      { id: number },
      { customerId: number; vehicleId: number; coverageType: CoverageType; sumInsured: number }
    >({
      query: (body) => ({ url: 'quotations', method: 'POST', body }),
      invalidatesTags: ['Quotation'],
    }),

    // ---------- Policies ----------
    getPolicies: build.query<
      PagedResult<PolicyDto>,
      { page?: number; pageSize?: number; status?: string; search?: string }
    >({
      query: (a) => `policies?${qs({ page: a.page, pageSize: a.pageSize, status: a.status, search: a.search })}`,
      providesTags: ['Policy'],
    }),
    getPolicy: build.query<PolicyDto, number>({
      query: (id) => `policies/${id}`,
      providesTags: (_r, _e, id) => [{ type: 'Policy', id }],
    }),
    getPolicyHistory: build.query<PolicyHistoryDto[], number>({
      query: (id) => `policies/${id}/history`,
      providesTags: (_r, _e, id) => [{ type: 'PolicyHistory', id }],
    }),
    issuePolicy: build.mutation<{ id: number }, { quotationId: number; effectiveDate: string }>({
      query: (body) => ({ url: 'policies/issue', method: 'POST', body }),
      invalidatesTags: ['Policy', 'Quotation', 'Payment'],
    }),
    activatePolicy: build.mutation<void, number>({
      query: (id) => ({ url: `policies/${id}/activate`, method: 'POST' }),
      invalidatesTags: (_r, _e, id) => ['Policy', { type: 'Policy', id }, { type: 'PolicyHistory', id }],
    }),
    cancelPolicy: build.mutation<void, { id: number; reason: string }>({
      query: ({ id, reason }) => ({ url: `policies/${id}/cancel`, method: 'POST', body: { reason } }),
      invalidatesTags: (_r, _e, { id }) => ['Policy', { type: 'Policy', id }, { type: 'PolicyHistory', id }],
    }),
    renewPolicy: build.mutation<{ id: number }, { policyId: number; adjustedSumInsured?: number }>({
      query: ({ policyId, adjustedSumInsured }) => ({
        url: `renewals/${policyId}`,
        method: 'POST',
        body: { adjustedSumInsured: adjustedSumInsured ?? null },
      }),
      invalidatesTags: ['Policy', 'Payment'],
    }),

    // ---------- Payments ----------
    getPayments: build.query<
      PagedResult<PaymentDto>,
      {
        page?: number;
        pageSize?: number;
        search?: string;
        status?: string;
        direction?: string;
        policyId?: number;
        claimId?: number;
      } | void
    >({
      query: (a) =>
        `payments?${qs({
          page: a?.page,
          pageSize: a?.pageSize,
          search: a?.search,
          status: a?.status,
          direction: a?.direction,
          policyId: a?.policyId,
          claimId: a?.claimId,
        })}`,
      providesTags: ['Payment'],
    }),
    settlePayment: build.mutation<void, { id: number; referenceNo: string }>({
      query: ({ id, referenceNo }) => ({ url: `payments/${id}/settle`, method: 'POST', body: { referenceNo } }),
      invalidatesTags: ['Payment', 'Policy', 'Claim'],
    }),

    // ---------- Claims ----------
    getClaims: build.query<
      PagedResult<ClaimDto>,
      { page?: number; pageSize?: number; search?: string; status?: string; policyId?: number } | void
    >({
      query: (a) =>
        `claims?${qs({ page: a?.page, pageSize: a?.pageSize, search: a?.search, status: a?.status, policyId: a?.policyId })}`,
      providesTags: ['Claim'],
    }),
    fileClaim: build.mutation<
      { id: number },
      { policyId: number; incidentDate: string; description?: string; claimedAmount: number }
    >({
      query: (body) => ({ url: 'claims', method: 'POST', body }),
      invalidatesTags: ['Claim'],
    }),
    advanceClaim: build.mutation<void, { id: number; to: ClaimStatus }>({
      query: ({ id, to }) => ({ url: `claims/${id}/advance`, method: 'POST', body: { to } }),
      invalidatesTags: ['Claim'],
    }),
    approveClaim: build.mutation<void, { id: number; approvedAmount: number }>({
      query: ({ id, approvedAmount }) => ({ url: `claims/${id}/approve`, method: 'POST', body: { approvedAmount } }),
      invalidatesTags: ['Claim'],
    }),
    rejectClaim: build.mutation<void, { id: number; reason: string }>({
      query: ({ id, reason }) => ({ url: `claims/${id}/reject`, method: 'POST', body: { reason } }),
      invalidatesTags: ['Claim'],
    }),

    // ---------- Dashboard ----------
    getDashboardSummary: build.query<DashboardSummary, void>({
      query: () => 'dashboard/summary',
      providesTags: ['Customer', 'Vehicle', 'Quotation', 'Policy', 'Claim', 'Payment'],
    }),
  }),
});

export const {
  useLoginMutation,
  useRefreshMutation,
  useLogoutMutation,
  useGetMeQuery,
  useGetCustomersQuery,
  useCreateCustomerMutation,
  useGetVehiclesQuery,
  useCreateVehicleMutation,
  useGetVehicleBrandsQuery,
  useGetVehicleModelsQuery,
  useGetVehicleSubmodelsQuery,
  useGetVehicleModelYearsQuery,
  useCreateBrandMutation,
  useUpdateBrandMutation,
  useDeleteBrandMutation,
  useCreateModelMutation,
  useUpdateModelMutation,
  useDeleteModelMutation,
  useCreateSubmodelMutation,
  useUpdateSubmodelMutation,
  useDeleteSubmodelMutation,
  useCreateModelYearMutation,
  useUpdateModelYearMutation,
  useDeleteModelYearMutation,
  useGetQuotationsQuery,
  useCreateQuotationMutation,
  useGetPoliciesQuery,
  useGetPolicyQuery,
  useGetPolicyHistoryQuery,
  useIssuePolicyMutation,
  useActivatePolicyMutation,
  useCancelPolicyMutation,
  useRenewPolicyMutation,
  useGetPaymentsQuery,
  useSettlePaymentMutation,
  useGetClaimsQuery,
  useFileClaimMutation,
  useAdvanceClaimMutation,
  useApproveClaimMutation,
  useRejectClaimMutation,
  useGetDashboardSummaryQuery,
} = insuranceApi;
