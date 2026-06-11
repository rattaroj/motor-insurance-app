'use client';

import { use } from 'react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { Pencil, Phone, Mail, Car, User } from 'lucide-react';
import { useGetCustomerOverviewQuery } from '@/lib/api/insuranceApi';
import { StatusBadge } from '@/components/StatusBadge';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
import { Skeleton } from '@/components/ui/skeleton';
import { Can } from '@/components/can';
import { PageHeader } from '@/components/page-header';
import { P } from '@/lib/auth/permissions';
import { fmtBaht, fmtDate, fmtDateTime } from '@/lib/utils';

export default function CustomerOverviewPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const router = useRouter();
  const { data, isLoading } = useGetCustomerOverviewQuery(Number(id));

  if (isLoading || !data) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-9 w-64" />
        <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
          {Array.from({ length: 4 }).map((_, i) => <Skeleton key={i} className="h-20 w-full" />)}
        </div>
        <Skeleton className="h-40 w-full" />
      </div>
    );
  }

  const stats = [
    { label: 'กรมธรรม์ทั้งหมด', value: data.stats.totalPolicies },
    { label: 'มีผลคุ้มครอง', value: data.stats.activePolicies },
    { label: 'เคลมที่เปิดอยู่', value: data.stats.openClaims },
    { label: 'เบี้ยที่ชำระแล้ว', value: fmtBaht(data.stats.premiumPaid) },
  ];

  return (
    <div className="space-y-6">
      <PageHeader
        icon={User}
        title={data.fullName}
        description={
          <span className="flex flex-wrap items-center gap-x-4 gap-y-1">
            <span className="tabular-nums">เลขบัตร {data.nationalId}</span>
            {data.phone && (
              <span className="inline-flex items-center gap-1">
                <Phone className="h-3.5 w-3.5" /> {data.phone}
              </span>
            )}
            {data.email && (
              <span className="inline-flex items-center gap-1">
                <Mail className="h-3.5 w-3.5" /> {data.email}
              </span>
            )}
          </span>
        }
        actions={
          <Can permission={P.CustomerWrite}>
            <Button variant="outline" onClick={() => router.push(`/customers/${id}/edit`)}>
              <Pencil /> แก้ไข
            </Button>
          </Can>
        }
      />

      {/* Headline stats */}
      <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
        {stats.map((s) => (
          <Card key={s.label}>
            <CardContent className="pt-6">
              <div className="text-2xl font-semibold tabular-nums">{s.value}</div>
              <div className="text-sm text-muted-foreground">{s.label}</div>
            </CardContent>
          </Card>
        ))}
      </div>
      {data.stats.outstanding > 0 && (
        <p className="text-sm text-amber-600">ยอดเบี้ยค้างชำระ {fmtBaht(data.stats.outstanding)}</p>
      )}

      {/* Vehicles */}
      <Section title="รถ">
        {data.vehicles.length === 0 ? (
          <Empty>ยังไม่มีรถ</Empty>
        ) : (
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
            {data.vehicles.map((v) => (
              <Card key={v.id}>
                <CardContent className="flex items-center gap-3 pt-6">
                  <Car className="h-5 w-5 text-muted-foreground" />
                  <div>
                    <div className="font-medium tabular-nums">{v.registrationNo}</div>
                    <div className="text-sm text-muted-foreground">
                      {v.brand} {v.model} · {v.year} · {v.province}
                    </div>
                  </div>
                </CardContent>
              </Card>
            ))}
          </div>
        )}
      </Section>

      {/* Policies */}
      <Section title="กรมธรรม์">
        {data.policies.length === 0 ? (
          <Empty>ยังไม่มีกรมธรรม์</Empty>
        ) : (
          <Card>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>เลขที่</TableHead>
                  <TableHead>ความคุ้มครอง</TableHead>
                  <TableHead>สถานะ</TableHead>
                  <TableHead>คุ้มครองถึง</TableHead>
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
                    <TableCell>{p.coverageType}</TableCell>
                    <TableCell><StatusBadge status={p.status} /></TableCell>
                    <TableCell>{fmtDate(p.expiryDate)}</TableCell>
                    <TableCell className="text-right tabular-nums">{fmtBaht(p.premium)}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </Card>
        )}
      </Section>

      {/* Claims */}
      <Section title="เคลม">
        {data.claims.length === 0 ? (
          <Empty>ยังไม่มีเคลม</Empty>
        ) : (
          <Card>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>เลขที่</TableHead>
                  <TableHead>กรมธรรม์</TableHead>
                  <TableHead>วันเกิดเหตุ</TableHead>
                  <TableHead>สถานะ</TableHead>
                  <TableHead className="text-right">เรียกร้อง</TableHead>
                  <TableHead className="text-right">อนุมัติ</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {data.claims.map((c) => (
                  <TableRow key={c.id}>
                    <TableCell className="font-medium">{c.claimNo}</TableCell>
                    <TableCell>{c.policyNo}</TableCell>
                    <TableCell>{fmtDate(c.incidentDate)}</TableCell>
                    <TableCell><StatusBadge status={c.status} /></TableCell>
                    <TableCell className="text-right tabular-nums">{fmtBaht(c.claimedAmount)}</TableCell>
                    <TableCell className="text-right tabular-nums">
                      {c.approvedAmount != null ? fmtBaht(c.approvedAmount) : '-'}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </Card>
        )}
      </Section>

      {/* Payments */}
      <Section title="ประวัติการชำระเงิน">
        {data.payments.length === 0 ? (
          <Empty>ยังไม่มีรายการชำระเงิน</Empty>
        ) : (
          <Card>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>เลขที่</TableHead>
                  <TableHead>ทิศทาง</TableHead>
                  <TableHead>สถานะ</TableHead>
                  <TableHead>ชำระเมื่อ</TableHead>
                  <TableHead className="text-right">จำนวน</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {data.payments.map((p) => (
                  <TableRow key={p.id}>
                    <TableCell className="font-medium">{p.paymentNo}</TableCell>
                    <TableCell>{p.direction === 'Inbound' ? 'รับเบี้ย' : 'จ่ายสินไหม'}</TableCell>
                    <TableCell><StatusBadge status={p.status} /></TableCell>
                    <TableCell>{p.paidAt ? fmtDateTime(p.paidAt) : '-'}</TableCell>
                    <TableCell className="text-right tabular-nums">{fmtBaht(p.amount)}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </Card>
        )}
      </Section>
    </div>
  );
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section className="space-y-3">
      <h2 className="text-lg font-medium">{title}</h2>
      {children}
    </section>
  );
}

function Empty({ children }: { children: React.ReactNode }) {
  return <p className="text-sm text-muted-foreground">{children}</p>;
}
