'use client';

import { Suspense } from 'react';
import Link from 'next/link';
import { useGetPoliciesQuery, useExportPoliciesMutation } from '@/lib/api/insuranceApi';
import { StatusBadge } from '@/components/StatusBadge';
import { ExportButton } from '@/components/export-button';
import { Card } from '@/components/ui/card';
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { PageHeader } from '@/components/page-header';
import { TablePagination } from '@/components/data-table';
import { Inbox, ShieldCheck } from 'lucide-react';
import { useListUrlState } from '@/lib/use-url-state';
import { fmtBaht } from '@/lib/utils';

const STATUSES = ['Draft', 'Quoted', 'Issued', 'Active', 'Cancelled', 'Expired'];

function PoliciesPageContent() {
  const { page, setPage, filters, setFilter } = useListUrlState(['status']);
  const status = filters.status;
  const { data, isLoading, isError, refetch } = useGetPoliciesQuery({
    page,
    pageSize: 10,
    status: status === 'all' ? undefined : status,
  });
  const [exportPolicies] = useExportPoliciesMutation();

  return (
    <div className="space-y-6">
      <PageHeader
        icon={ShieldCheck}
        title="กรมธรรม์"
        description="รายการกรมธรรม์ทั้งหมด"
        actions={
          <>
            <Select value={status} onValueChange={(v) => setFilter('status', v)}>
              <SelectTrigger className="w-44">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">ทุกสถานะ</SelectItem>
                {STATUSES.map((s) => (
                  <SelectItem key={s} value={s}>
                    {s}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <ExportButton
              filename="policies.csv"
              fetchUrl={() => exportPolicies({ status: status === 'all' ? undefined : status }).unwrap()}
            />
          </>
        }
      />

      {isError && (
        <Card className="border-destructive/40 bg-destructive/5 p-4 text-sm text-destructive">
          โหลดข้อมูลไม่สำเร็จ — ตรวจว่า backend รันอยู่
          <button onClick={() => refetch()} className="ml-2 underline">
            ลองใหม่
          </button>
        </Card>
      )}

      <Card className="overflow-hidden">
        <Table>
          <TableHeader>
            <TableRow className="bg-muted/50 hover:bg-muted/50">
              <TableHead>เลขกรมธรรม์</TableHead>
              <TableHead>ลูกค้า</TableHead>
              <TableHead>ทะเบียน</TableHead>
              <TableHead>สถานะ</TableHead>
              <TableHead className="text-right">เบี้ย</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {isLoading &&
              Array.from({ length: 8 }).map((_, r) => (
                <TableRow key={`skeleton-${r}`}>
                  <TableCell>
                    <Skeleton className="h-4 w-28" />
                  </TableCell>
                  <TableCell>
                    <Skeleton className="h-4 w-32" />
                  </TableCell>
                  <TableCell>
                    <Skeleton className="h-4 w-20" />
                  </TableCell>
                  <TableCell>
                    <Skeleton className="h-5 w-16 rounded-full" />
                  </TableCell>
                  <TableCell className="text-right">
                    <Skeleton className="ml-auto h-4 w-16" />
                  </TableCell>
                </TableRow>
              ))}
            {data?.items.map((p) => (
              <TableRow key={p.id}>
                <TableCell>
                  <Link href={`/policies/${p.id}`} className="font-medium text-primary hover:underline">
                    {p.policyNo}
                  </Link>
                </TableCell>
                <TableCell>{p.customerName}</TableCell>
                <TableCell>{p.vehicleRegistration}</TableCell>
                <TableCell>
                  <StatusBadge status={p.status} />
                </TableCell>
                <TableCell className="text-right tabular-nums">{fmtBaht(p.premium)}</TableCell>
              </TableRow>
            ))}
            {data?.items.length === 0 && (
              <TableRow className="hover:bg-transparent">
                <TableCell colSpan={5}>
                  <div className="flex flex-col items-center gap-2 py-10 text-muted-foreground">
                    <span className="flex h-12 w-12 items-center justify-center rounded-full bg-muted">
                      <Inbox className="h-6 w-6 text-muted-foreground/60" />
                    </span>
                    <span className="text-sm">ไม่มีข้อมูล</span>
                  </div>
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </Card>

      {data && (
        <TablePagination
          page={page}
          totalPages={data.totalPages || 1}
          totalCount={data.totalCount}
          onPageChange={setPage}
        />
      )}
    </div>
  );
}

export default function PoliciesPage() {
  return (
    <Suspense fallback={null}>
      <PoliciesPageContent />
    </Suspense>
  );
}
