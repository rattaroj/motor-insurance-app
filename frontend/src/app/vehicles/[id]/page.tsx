'use client';

import { use, useState } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { toast } from 'sonner';
import { ArrowLeft, Car, Pencil, Trash2, User } from 'lucide-react';
import {
  useGetVehicleQuery,
  useDeleteVehicleMutation,
  POWERTRAIN_LABELS,
} from '@/lib/api/insuranceApi';
import { StatusBadge } from '@/components/StatusBadge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
} from '@/components/ui/dialog';
import { Skeleton } from '@/components/ui/skeleton';
import { Can } from '@/components/can';
import { PageHeader } from '@/components/page-header';
import { AuditFooter } from '@/components/audit-footer';
import { P } from '@/lib/auth/permissions';
import { apiError, fmtBaht, fmtDate } from '@/lib/utils';

function Fact({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <Card>
      <CardContent className="p-4">
        <p className="text-xs text-muted-foreground">{label}</p>
        <div className="mt-1 font-medium">{value}</div>
      </CardContent>
    </Card>
  );
}

export default function VehicleDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const vehicleId = Number(id);
  const router = useRouter();
  const { data, isLoading } = useGetVehicleQuery(vehicleId);
  const [deleteVehicle, { isLoading: deleting }] = useDeleteVehicleMutation();
  const [confirmDelete, setConfirmDelete] = useState(false);

  const doDelete = async () => {
    try {
      await deleteVehicle(vehicleId).unwrap();
      toast.success('ลบรถยนต์แล้ว');
      router.push('/vehicles');
    } catch (e) {
      toast.error(apiError(e));
    }
  };

  if (isLoading || !data) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-6 w-24" />
        <Skeleton className="h-24 w-full" />
        <div className="grid gap-4 sm:grid-cols-4">
          {Array.from({ length: 4 }).map((_, i) => (
            <Skeleton key={i} className="h-20 w-full" />
          ))}
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <Link href="/vehicles" className="inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground">
        <ArrowLeft className="h-4 w-4" /> กลับ
      </Link>

      <PageHeader
        icon={Car}
        title={data.registrationNo}
        description={
          <>
            {data.province} · {data.brand} {data.model} {data.submodel} · ปี {data.year}
          </>
        }
        actions={
          <Can permission={P.VehicleWrite}>
            <Button variant="outline" onClick={() => router.push(`/vehicles/${vehicleId}/edit`)}>
              <Pencil /> แก้ไข
            </Button>
            <Button variant="destructive" onClick={() => setConfirmDelete(true)}>
              <Trash2 /> ลบ
            </Button>
          </Can>
        }
      />

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <Fact
          label="เจ้าของ"
          value={
            <Link href={`/customers/${data.customerId}`} className="inline-flex items-center gap-1 text-primary hover:underline">
              <User className="h-3.5 w-3.5" /> {data.customerName}
            </Link>
          }
        />
        <Fact label="ยี่ห้อ / รุ่น" value={`${data.brand} ${data.model}`} />
        <Fact label="พลังงาน" value={POWERTRAIN_LABELS[data.powertrain]} />
        <Fact label="เลขตัวถัง" value={data.chassisNo ?? <span className="text-muted-foreground">—</span>} />
      </div>

      <Card>
        <Table>
          <TableHeader>
            <TableRow className="bg-muted/50 hover:bg-muted/50">
              <TableHead>เลขกรมธรรม์</TableHead>
              <TableHead>สถานะ</TableHead>
              <TableHead>ความคุ้มครอง</TableHead>
              <TableHead>คุ้มครอง</TableHead>
              <TableHead className="text-right">เบี้ย</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {data.policies.map((p) => (
              <TableRow key={p.id}>
                <TableCell>
                  <Link href={`/policies/${p.id}`} className="font-medium text-primary hover:underline">
                    {p.policyNo}
                  </Link>
                </TableCell>
                <TableCell>
                  <StatusBadge status={p.status} />
                </TableCell>
                <TableCell>{p.coverageType}</TableCell>
                <TableCell className="text-muted-foreground">
                  {p.effectiveDate ? `${fmtDate(p.effectiveDate)} – ${fmtDate(p.expiryDate)}` : '—'}
                </TableCell>
                <TableCell className="text-right tabular-nums">{fmtBaht(p.premium)}</TableCell>
              </TableRow>
            ))}
            {data.policies.length === 0 && (
              <TableRow className="hover:bg-transparent">
                <TableCell colSpan={5} className="py-8 text-center text-muted-foreground">
                  รถคันนี้ยังไม่มีกรมธรรม์
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </Card>

      <AuditFooter audit={data.audit} />

      <Dialog open={confirmDelete} onOpenChange={setConfirmDelete}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>ลบรถยนต์</DialogTitle>
            <DialogDescription>
              ต้องการลบ {data.registrationNo} ({data.brand} {data.model}) หรือไม่? ลบได้เฉพาะรถที่ยังไม่มีใบเสนอราคา/กรมธรรม์
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setConfirmDelete(false)}>
              ยกเลิก
            </Button>
            <Button variant="destructive" onClick={doDelete} disabled={deleting}>
              {deleting ? 'กำลังลบ…' : 'ลบ'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
