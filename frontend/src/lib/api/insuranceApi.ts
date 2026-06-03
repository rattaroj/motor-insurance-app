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
  title: string | null;
  firstName: string;
  lastName: string;
  fullName: string;
  birthDate: string | null;
  phone: string | null;
  email: string | null;
  addressLine: string | null;
  provinceId: number | null;
  provinceName: string | null;
  districtId: number | null;
  districtName: string | null;
  subdistrictId: number | null;
  subdistrictName: string | null;
  postalCodeId: number | null;
  postalCode: string | null;
}

/** Address fields sent on customer create/update — all optional. */
export interface CustomerAddressInput {
  addressLine?: string;
  provinceId?: number;
  districtId?: number;
  subdistrictId?: number;
  postalCodeId?: number;
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

// Thai administrative-division lookups (bilingual; subdistrict carries its postal code).
export interface GeoOption {
  id: number;
  nameTh: string;
  nameEn: string;
}

export interface SubdistrictOption {
  id: number;
  nameTh: string;
  nameEn: string;
  postalCodeId: number;
  postalCode: string;
}

export interface PostalCodeOption {
  id: number;
  code: string;
}

/** Add-on rider (ความคุ้มครองเสริม) master row. */
export interface Rider {
  id: number;
  name: string;
  premium: number;
}

/** Premium broken down by factor (live preview + create response context). */
export interface PremiumBreakdown {
  basePremium: number;
  vehicleAgeLoading: number;
  ncbDiscount: number;
  deductibleDiscount: number;
  ridersTotal: number;
  netPremium: number;
}

/** Allowed no-claim-bonus discount steps (mirrors backend PremiumCalculator.NcbSteps). */
export const NCB_STEPS = [0, 20, 30, 40, 50] as const;

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
  basePremium: number;
  ncbPercent: number;
  deductible: number;
  riders: string[];
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
  basePremium: number;
  ncbPercent: number;
  deductible: number;
  effectiveDate: string | null;
  expiryDate: string | null;
  previousPolicyId: number | null;
}

export interface DriverInput {
  fullName: string;
  nationalId: string;
  idCardImagePath: string;
}

export interface PolicyDriverDto {
  fullName: string;
  nationalId: string;
  idCardImagePath: string;
}

export interface EndorsementDto {
  endorsementNo: string;
  fieldName: string;
  oldValue: string | null;
  newValue: string | null;
  effectiveDate: string;
  note: string | null;
  createdAt: string;
}

/** Policy detail = the list DTO plus riders, named drivers and endorsement history. */
export interface PolicyDetailDto extends PolicyDto {
  riders: string[];
  drivers: PolicyDriverDto[];
  endorsements: EndorsementDto[];
}

export interface PolicyHistoryDto {
  status: string;
  premium: number;
  validFrom: string;
  validTo: string;
}

/** A policy in the proactive renewal worklist (expiring soon, not yet renewed). */
export interface ExpiringPolicy {
  policyId: number;
  policyNo: string;
  customerName: string;
  customerEmail: string | null;
  customerPhone: string | null;
  expiryDate: string;
  daysLeft: number;
  lastRemindedAt: string | null;
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
  garageId: number | null;
  garageName: string | null;
  surveyorName: string | null;
  photoCount: number;
}

/** Repair-shop (อู่/ศูนย์ซ่อม) master row. */
export interface Garage {
  id: number;
  name: string;
  phone: string | null;
}

export interface ClaimPhoto {
  id: number;
  imagePath: string;
  createdAt: string;
}

/** Claim detail = core claim fields plus garage contact, surveyor and damage photos. */
export interface ClaimDetailDto {
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
  garageId: number | null;
  garageName: string | null;
  garagePhone: string | null;
  surveyorName: string | null;
  photos: ClaimPhoto[];
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

export interface MonthPremium {
  month: string;
  premium: number;
}
export interface LabelCount {
  label: string;
  count: number;
}
export interface Analytics {
  premiumWritten: number;
  claimsPaid: number;
  lossRatio: number;
  premiumByMonth: MonthPremium[];
  policiesByStatus: LabelCount[];
  policiesByCoverage: LabelCount[];
  claimsByStatus: LabelCount[];
}

const baseUrl = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000/api';

/** Absolute URL for an uploaded file (served from the host root, not under /api). */
export const fileUrl = (path: string) => `${baseUrl.replace(/\/api\/?$/, '')}/${path}`;

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
    'CustomerTitle',
    'Rider',
    'Garage',
    'Renewal',
    'VBrand',
    'VModel',
    'VSubmodel',
    'VYear',
    'Province',
    'District',
    'Subdistrict',
    'PostalCode',
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
    getCustomer: build.query<CustomerDto, number>({
      query: (id) => `customers/${id}`,
      providesTags: (_r, _e, id) => [{ type: 'Customer', id }],
    }),
    createCustomer: build.mutation<
      { id: number },
      {
        nationalId: string;
        title?: string;
        firstName: string;
        lastName: string;
        birthDate?: string;
        phone?: string;
        email?: string;
      } & CustomerAddressInput
    >({
      query: (body) => ({ url: 'customers', method: 'POST', body }),
      invalidatesTags: ['Customer'],
    }),
    updateCustomer: build.mutation<
      void,
      {
        id: number;
        title?: string;
        firstName: string;
        lastName: string;
        birthDate?: string;
        phone?: string;
        email?: string;
      } & CustomerAddressInput
    >({
      query: ({ id, ...body }) => ({ url: `customers/${id}`, method: 'PUT', body }),
      invalidatesTags: ['Customer'],
    }),
    deleteCustomer: build.mutation<void, number>({
      query: (id) => ({ url: `customers/${id}`, method: 'DELETE' }),
      invalidatesTags: ['Customer'],
    }),

    // ---------- Customer title master-data ----------
    getCustomerTitles: build.query<Option[], void>({
      query: () => 'lookups/customer-titles',
      providesTags: ['CustomerTitle'],
    }),
    createCustomerTitle: build.mutation<{ id: number }, { name: string }>({
      query: (body) => ({ url: 'lookups/customer-titles', method: 'POST', body }),
      invalidatesTags: ['CustomerTitle'],
    }),
    updateCustomerTitle: build.mutation<void, { id: number; name: string }>({
      query: ({ id, name }) => ({ url: `lookups/customer-titles/${id}`, method: 'PUT', body: { name } }),
      invalidatesTags: ['CustomerTitle'],
    }),
    deleteCustomerTitle: build.mutation<void, number>({
      query: (id) => ({ url: `lookups/customer-titles/${id}`, method: 'DELETE' }),
      invalidatesTags: ['CustomerTitle'],
    }),

    // ---------- Uploads ----------
    uploadIdCard: build.mutation<{ path: string }, File>({
      query: (file) => {
        const form = new FormData();
        form.append('file', file);
        return { url: 'uploads/id-card', method: 'POST', body: form };
      },
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

    // ---------- Thai administrative-division lookups (cascading: province -> district -> subdistrict + postal code) ----------
    getProvinces: build.query<GeoOption[], void>({
      query: () => 'lookups/provinces',
      providesTags: ['Province'],
    }),
    getDistricts: build.query<GeoOption[], number>({
      query: (provinceId) => `lookups/districts?provinceId=${provinceId}`,
      providesTags: ['District'],
    }),
    getSubdistricts: build.query<SubdistrictOption[], number>({
      query: (districtId) => `lookups/subdistricts?districtId=${districtId}`,
      providesTags: ['Subdistrict'],
    }),
    getPostalCodes: build.query<PostalCodeOption[], void>({
      query: () => 'lookups/postal-codes',
      providesTags: ['PostalCode'],
    }),

    createProvince: build.mutation<{ id: number }, { id: number; nameTh: string; nameEn: string }>({
      query: (body) => ({ url: 'lookups/provinces', method: 'POST', body }),
      invalidatesTags: ['Province'],
    }),
    updateProvince: build.mutation<void, { id: number; nameTh: string; nameEn: string }>({
      query: ({ id, ...body }) => ({ url: `lookups/provinces/${id}`, method: 'PUT', body }),
      invalidatesTags: ['Province'],
    }),
    deleteProvince: build.mutation<void, number>({
      query: (id) => ({ url: `lookups/provinces/${id}`, method: 'DELETE' }),
      invalidatesTags: ['Province'],
    }),

    createDistrict: build.mutation<{ id: number }, { id: number; provinceId: number; nameTh: string; nameEn: string }>({
      query: (body) => ({ url: 'lookups/districts', method: 'POST', body }),
      invalidatesTags: ['District'],
    }),
    updateDistrict: build.mutation<void, { id: number; nameTh: string; nameEn: string }>({
      query: ({ id, ...body }) => ({ url: `lookups/districts/${id}`, method: 'PUT', body }),
      invalidatesTags: ['District'],
    }),
    deleteDistrict: build.mutation<void, number>({
      query: (id) => ({ url: `lookups/districts/${id}`, method: 'DELETE' }),
      invalidatesTags: ['District'],
    }),

    createSubdistrict: build.mutation<
      { id: number },
      { id: number; districtId: number; postalCodeId: number; nameTh: string; nameEn: string }
    >({
      query: (body) => ({ url: 'lookups/subdistricts', method: 'POST', body }),
      invalidatesTags: ['Subdistrict'],
    }),
    updateSubdistrict: build.mutation<void, { id: number; nameTh: string; nameEn: string; postalCodeId: number }>({
      query: ({ id, ...body }) => ({ url: `lookups/subdistricts/${id}`, method: 'PUT', body }),
      invalidatesTags: ['Subdistrict'],
    }),
    deleteSubdistrict: build.mutation<void, number>({
      query: (id) => ({ url: `lookups/subdistricts/${id}`, method: 'DELETE' }),
      invalidatesTags: ['Subdistrict'],
    }),

    createPostalCode: build.mutation<{ id: number }, { code: string }>({
      query: (body) => ({ url: 'lookups/postal-codes', method: 'POST', body }),
      invalidatesTags: ['PostalCode'],
    }),
    updatePostalCode: build.mutation<void, { id: number; code: string }>({
      query: ({ id, code }) => ({ url: `lookups/postal-codes/${id}`, method: 'PUT', body: { code } }),
      invalidatesTags: ['PostalCode'],
    }),
    deletePostalCode: build.mutation<void, number>({
      query: (id) => ({ url: `lookups/postal-codes/${id}`, method: 'DELETE' }),
      invalidatesTags: ['PostalCode'],
    }),

    // ---------- Quotations ----------
    getQuotations: build.query<PagedResult<QuotationDto>, { page?: number; pageSize?: number; search?: string } | void>({
      query: (a) => `quotations?${qs({ page: a?.page, pageSize: a?.pageSize, search: a?.search })}`,
      providesTags: ['Quotation'],
    }),
    createQuotation: build.mutation<
      { id: number },
      {
        customerId: number;
        vehicleId: number;
        coverageType: CoverageType;
        sumInsured: number;
        drivers: DriverInput[];
        ncbPercent?: number;
        deductible?: number;
        riderIds?: number[];
      }
    >({
      query: (body) => ({ url: 'quotations', method: 'POST', body }),
      invalidatesTags: ['Quotation'],
    }),
    /** Live premium preview — rate without persisting (for the create form). */
    previewPremium: build.mutation<
      PremiumBreakdown,
      {
        vehicleId: number;
        coverageType: CoverageType;
        sumInsured: number;
        ncbPercent?: number;
        deductible?: number;
        riderIds?: number[];
      }
    >({
      query: (body) => ({ url: 'quotations/preview', method: 'POST', body }),
    }),

    // ---------- Riders (add-on coverage master) ----------
    getRiders: build.query<Rider[], void>({
      query: () => 'lookups/riders',
      providesTags: ['Rider'],
    }),
    createRider: build.mutation<{ id: number }, { name: string; premium: number }>({
      query: (body) => ({ url: 'lookups/riders', method: 'POST', body }),
      invalidatesTags: ['Rider'],
    }),
    updateRider: build.mutation<void, { id: number; name: string; premium: number }>({
      query: ({ id, name, premium }) => ({ url: `lookups/riders/${id}`, method: 'PUT', body: { name, premium } }),
      invalidatesTags: ['Rider'],
    }),
    deleteRider: build.mutation<void, number>({
      query: (id) => ({ url: `lookups/riders/${id}`, method: 'DELETE' }),
      invalidatesTags: ['Rider'],
    }),

    // ---------- Policies ----------
    getPolicies: build.query<
      PagedResult<PolicyDto>,
      { page?: number; pageSize?: number; status?: string; search?: string }
    >({
      query: (a) => `policies?${qs({ page: a.page, pageSize: a.pageSize, status: a.status, search: a.search })}`,
      providesTags: ['Policy'],
    }),
    getPolicy: build.query<PolicyDetailDto, number>({
      query: (id) => `policies/${id}`,
      providesTags: (_r, _e, id) => [{ type: 'Policy', id }],
    }),
    getPolicyHistory: build.query<PolicyHistoryDto[], number>({
      query: (id) => `policies/${id}/history`,
      providesTags: (_r, _e, id) => [{ type: 'PolicyHistory', id }],
    }),
    /** Policy schedule PDF (ตารางกรมธรรม์) → object URL (kept serializable in the store). */
    getPolicyDocument: build.mutation<string, number>({
      query: (id) => ({ url: `policies/${id}/document`, responseHandler: (r) => r.blob() }),
      transformResponse: (blob: Blob) => URL.createObjectURL(blob),
    }),
    issuePolicy: build.mutation<{ id: number }, { quotationId: number; effectiveDate: string }>({
      query: (body) => ({ url: 'policies/issue', method: 'POST', body }),
      invalidatesTags: ['Policy', 'Quotation', 'Payment'],
    }),
    activatePolicy: build.mutation<void, number>({
      query: (id) => ({ url: `policies/${id}/activate`, method: 'POST' }),
      invalidatesTags: (_r, _e, id) => ['Policy', { type: 'Policy', id }, { type: 'PolicyHistory', id }],
    }),
    cancelPolicy: build.mutation<
      { refundAmount: number; refundPaymentNo: string | null },
      { id: number; reason: string }
    >({
      query: ({ id, reason }) => ({ url: `policies/${id}/cancel`, method: 'POST', body: { reason } }),
      invalidatesTags: (_r, _e, { id }) => [
        'Policy', 'Payment', { type: 'Policy', id }, { type: 'PolicyHistory', id },
      ],
    }),
    renewPolicy: build.mutation<{ id: number }, { policyId: number; adjustedSumInsured?: number }>({
      query: ({ policyId, adjustedSumInsured }) => ({
        url: `renewals/${policyId}`,
        method: 'POST',
        body: { adjustedSumInsured: adjustedSumInsured ?? null },
      }),
      invalidatesTags: ['Policy', 'Payment', 'Renewal'],
    }),
    getExpiringPolicies: build.query<ExpiringPolicy[], { days?: number } | void>({
      query: (a) => `renewals/expiring${a?.days ? `?days=${a.days}` : ''}`,
      providesTags: ['Renewal'],
    }),
    sendRenewalReminder: build.mutation<
      { notificationId: number; channel: string; recipient: string; status: string },
      number
    >({
      query: (policyId) => ({ url: `renewals/${policyId}/remind`, method: 'POST' }),
      invalidatesTags: ['Renewal'],
    }),
    createEndorsement: build.mutation<
      { endorsementNos: string[] },
      { policyId: number; fullName?: string; phone?: string; email?: string; effectiveDate: string; note?: string }
    >({
      query: ({ policyId, ...body }) => ({
        url: `policies/${policyId}/endorsements`,
        method: 'POST',
        body,
      }),
      invalidatesTags: (_r, _e, { policyId }) => [
        'Customer',
        'Policy',
        { type: 'Policy', id: policyId },
        { type: 'PolicyHistory', id: policyId },
      ],
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
    /** Premium receipt PDF (ใบเสร็จรับเงิน) → object URL. */
    getPaymentReceipt: build.mutation<string, number>({
      query: (id) => ({ url: `payments/${id}/receipt`, responseHandler: (r) => r.blob() }),
      transformResponse: (blob: Blob) => URL.createObjectURL(blob),
    }),
    /** PromptPay QR PNG for a pending inbound premium → object URL. */
    getPromptPayQr: build.mutation<string, number>({
      query: (id) => ({ url: `payments/${id}/promptpay-qr`, responseHandler: (r) => r.blob() }),
      transformResponse: (blob: Blob) => URL.createObjectURL(blob),
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
    getClaim: build.query<ClaimDetailDto, number>({
      query: (id) => `claims/${id}`,
      providesTags: (_r, _e, id) => [{ type: 'Claim', id }],
    }),
    assignClaim: build.mutation<void, { id: number; garageId?: number | null; surveyorName?: string | null }>({
      query: ({ id, garageId, surveyorName }) => ({
        url: `claims/${id}/assign`,
        method: 'POST',
        body: { garageId: garageId ?? null, surveyorName: surveyorName ?? null },
      }),
      invalidatesTags: (_r, _e, { id }) => ['Claim', { type: 'Claim', id }],
    }),
    uploadClaimPhoto: build.mutation<ClaimPhoto, { id: number; file: File }>({
      query: ({ id, file }) => {
        const form = new FormData();
        form.append('file', file);
        return { url: `claims/${id}/photos`, method: 'POST', body: form };
      },
      invalidatesTags: (_r, _e, { id }) => ['Claim', { type: 'Claim', id }],
    }),

    // ---------- Garages (repair-shop master) ----------
    getGarages: build.query<Garage[], void>({
      query: () => 'lookups/garages',
      providesTags: ['Garage'],
    }),
    createGarage: build.mutation<{ id: number }, { name: string; phone?: string }>({
      query: (body) => ({ url: 'lookups/garages', method: 'POST', body }),
      invalidatesTags: ['Garage'],
    }),
    updateGarage: build.mutation<void, { id: number; name: string; phone?: string }>({
      query: ({ id, name, phone }) => ({ url: `lookups/garages/${id}`, method: 'PUT', body: { name, phone } }),
      invalidatesTags: ['Garage'],
    }),
    deleteGarage: build.mutation<void, number>({
      query: (id) => ({ url: `lookups/garages/${id}`, method: 'DELETE' }),
      invalidatesTags: ['Garage'],
    }),

    // ---------- Dashboard ----------
    getDashboardSummary: build.query<DashboardSummary, void>({
      query: () => 'dashboard/summary',
      providesTags: ['Customer', 'Vehicle', 'Quotation', 'Policy', 'Claim', 'Payment'],
    }),
    getAnalytics: build.query<Analytics, void>({
      query: () => 'reports/analytics',
      providesTags: ['Policy', 'Claim', 'Payment'],
    }),
  }),
});

export const {
  useLoginMutation,
  useRefreshMutation,
  useLogoutMutation,
  useGetMeQuery,
  useGetCustomersQuery,
  useGetCustomerQuery,
  useCreateCustomerMutation,
  useUpdateCustomerMutation,
  useDeleteCustomerMutation,
  useGetCustomerTitlesQuery,
  useCreateCustomerTitleMutation,
  useUpdateCustomerTitleMutation,
  useDeleteCustomerTitleMutation,
  useUploadIdCardMutation,
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
  useGetProvincesQuery,
  useGetDistrictsQuery,
  useGetSubdistrictsQuery,
  useGetPostalCodesQuery,
  useCreateProvinceMutation,
  useUpdateProvinceMutation,
  useDeleteProvinceMutation,
  useCreateDistrictMutation,
  useUpdateDistrictMutation,
  useDeleteDistrictMutation,
  useCreateSubdistrictMutation,
  useUpdateSubdistrictMutation,
  useDeleteSubdistrictMutation,
  useCreatePostalCodeMutation,
  useUpdatePostalCodeMutation,
  useDeletePostalCodeMutation,
  useGetQuotationsQuery,
  useCreateQuotationMutation,
  usePreviewPremiumMutation,
  useGetRidersQuery,
  useCreateRiderMutation,
  useUpdateRiderMutation,
  useDeleteRiderMutation,
  useGetPoliciesQuery,
  useGetPolicyQuery,
  useGetPolicyHistoryQuery,
  useGetPolicyDocumentMutation,
  useGetPaymentReceiptMutation,
  useGetPromptPayQrMutation,
  useIssuePolicyMutation,
  useActivatePolicyMutation,
  useCancelPolicyMutation,
  useRenewPolicyMutation,
  useGetExpiringPoliciesQuery,
  useSendRenewalReminderMutation,
  useCreateEndorsementMutation,
  useGetPaymentsQuery,
  useSettlePaymentMutation,
  useGetClaimsQuery,
  useFileClaimMutation,
  useAdvanceClaimMutation,
  useApproveClaimMutation,
  useRejectClaimMutation,
  useGetClaimQuery,
  useAssignClaimMutation,
  useUploadClaimPhotoMutation,
  useGetGaragesQuery,
  useCreateGarageMutation,
  useUpdateGarageMutation,
  useDeleteGarageMutation,
  useGetDashboardSummaryQuery,
  useGetAnalyticsQuery,
} = insuranceApi;
