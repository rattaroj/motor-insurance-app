'use client';

import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { toast } from 'sonner';
import { ArrowLeft } from 'lucide-react';
import { useCreateCustomerMutation } from '@/lib/api/insuranceApi';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { CustomerForm, customerPayload, type CustomerFormValues } from '@/components/customer-form';
import { apiError } from '@/lib/utils';

export default function NewCustomerPage() {
  const router = useRouter();
  const [createCustomer, { isLoading }] = useCreateCustomerMutation();

  const submit = async (v: CustomerFormValues) => {
    try {
      await createCustomer({ nationalId: v.nationalId, ...customerPayload(v) }).unwrap();
      toast.success('เพิ่มลูกค้าแล้ว');
      router.push('/customers');
    } catch (e) {
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
        <h1 className="text-2xl font-semibold tracking-tight">เพิ่มลูกค้า</h1>
        <p className="text-sm text-muted-foreground">กรอกข้อมูลผู้เอาประกันรายใหม่</p>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">ข้อมูลลูกค้า</CardTitle>
        </CardHeader>
        <CardContent>
          <CustomerForm mode="create" submitting={isLoading} onSubmit={submit} />
        </CardContent>
      </Card>
    </div>
  );
}
