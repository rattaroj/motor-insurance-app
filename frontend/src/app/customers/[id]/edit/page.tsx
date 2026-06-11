'use client';

import { use, useMemo } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { toast } from 'sonner';
import { ArrowLeft, UserCog } from 'lucide-react';
import { useGetCustomerQuery, useUpdateCustomerMutation } from '@/lib/api/insuranceApi';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { CustomerForm, customerPayload, type CustomerFormValues } from '@/components/customer-form';
import { PageHeader } from '@/components/page-header';
import { apiError } from '@/lib/utils';

export default function EditCustomerPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const customerId = Number(id);
  const router = useRouter();

  const { data: customer, isLoading, isError } = useGetCustomerQuery(customerId);
  const [updateCustomer, { isLoading: saving }] = useUpdateCustomerMutation();

  // Map the loaded customer into the form's value shape (re-runs when it loads).
  const initial = useMemo<CustomerFormValues | undefined>(
    () =>
      customer && {
        nationalId: customer.nationalId,
        title: customer.title ?? '',
        firstName: customer.firstName,
        lastName: customer.lastName,
        birthDate: customer.birthDate ?? '',
        phone: customer.phone ?? '',
        email: customer.email ?? '',
        lineUserId: customer.lineUserId ?? '',
        addressLine: customer.addressLine ?? '',
        address: {
          provinceId: customer.provinceId,
          districtId: customer.districtId,
          subdistrictId: customer.subdistrictId,
          postalCodeId: customer.postalCodeId,
          postalCode: customer.postalCode,
        },
      },
    [customer],
  );

  const submit = async (v: CustomerFormValues) => {
    try {
      await updateCustomer({ id: customerId, ...customerPayload(v) }).unwrap();
      toast.success('แก้ไขข้อมูลลูกค้าแล้ว');
      router.push('/customers');
    } catch (e) {
      // 409 = ลูกค้ามีกรมธรรม์แล้ว ต้องแก้ไขผ่านการสลักหลังที่หน้ากรมธรรม์
      toast.error(apiError(e));
    }
  };

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      <div>
        <Button variant="ghost" size="sm" asChild className="mb-2 -ml-2">
          <Link href="/customers">
            <ArrowLeft /> กลับไปหน้าลูกค้า
          </Link>
        </Button>
        <PageHeader
          icon={UserCog}
          title="แก้ไขข้อมูลลูกค้า"
          description={
            customer && (
              <>เลขบัตร {customer.nationalId} — หากลูกค้ามีกรมธรรม์แล้ว ต้องแก้ไขผ่านการสลักหลังที่หน้ากรมธรรม์</>
            )
          }
        />
      </div>

      {isLoading ? (
        <Card>
          <CardContent className="space-y-4 py-6">
            <Skeleton className="h-10 w-full" />
            <Skeleton className="h-10 w-full" />
            <Skeleton className="h-24 w-full" />
          </CardContent>
        </Card>
      ) : isError || !customer ? (
        <Card>
          <CardContent className="py-10 text-center text-sm text-muted-foreground">ไม่พบลูกค้ารายนี้</CardContent>
        </Card>
      ) : (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">ข้อมูลลูกค้า</CardTitle>
          </CardHeader>
          <CardContent>
            <CustomerForm mode="edit" initial={initial} submitting={saving} onSubmit={submit} />
          </CardContent>
        </Card>
      )}
    </div>
  );
}
