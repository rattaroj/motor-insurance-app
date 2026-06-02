'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { TITLE_OPTIONS } from '@/lib/api/insuranceApi';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { AddressSelect, emptyAddress, type AddressValue } from '@/components/address-select';

/** Editable customer fields shared by the create and edit pages. */
export interface CustomerFormValues {
  nationalId: string;
  title: string;
  firstName: string;
  lastName: string;
  birthDate: string;
  phone: string;
  email: string;
  addressLine: string;
  address: AddressValue;
}

export const emptyCustomerForm: CustomerFormValues = {
  nationalId: '',
  title: '',
  firstName: '',
  lastName: '',
  birthDate: '',
  phone: '',
  email: '',
  addressLine: '',
  address: emptyAddress,
};

/** Map the form values to the common create/update API payload (omitting empty optionals). */
export function customerPayload(v: CustomerFormValues) {
  return {
    title: v.title || undefined,
    firstName: v.firstName,
    lastName: v.lastName,
    birthDate: v.birthDate || undefined,
    phone: v.phone || undefined,
    email: v.email || undefined,
    addressLine: v.addressLine || undefined,
    provinceId: v.address.provinceId ?? undefined,
    districtId: v.address.districtId ?? undefined,
    subdistrictId: v.address.subdistrictId ?? undefined,
    postalCodeId: v.address.postalCodeId ?? undefined,
  };
}

interface CustomerFormProps {
  mode: 'create' | 'edit';
  /** Initial values (edit prefill). May arrive asynchronously; the form re-syncs when it changes. */
  initial?: CustomerFormValues;
  submitting: boolean;
  onSubmit: (values: CustomerFormValues) => void;
}

/**
 * The customer create/edit form body (fields + actions). National id is shown only in
 * create mode (it is the identity key and not editable afterwards). Cancel returns to the list.
 */
export function CustomerForm({ mode, initial, submitting, onSubmit }: CustomerFormProps) {
  const [v, setV] = useState<CustomerFormValues>(initial ?? emptyCustomerForm);
  useEffect(() => {
    if (initial) setV(initial);
  }, [initial]);

  const set = (patch: Partial<CustomerFormValues>) => setV((s) => ({ ...s, ...patch }));
  const valid =
    v.firstName.trim() !== '' && v.lastName.trim() !== '' && (mode === 'edit' || v.nationalId.trim() !== '');

  return (
    <div className="space-y-4">
      {mode === 'create' && (
        <div className="space-y-2">
          <Label htmlFor="nationalId" required>เลขบัตรประชาชน (13 หลัก)</Label>
          <Input
            id="nationalId"
            value={v.nationalId}
            onChange={(e) => set({ nationalId: e.target.value })}
            maxLength={13}
            placeholder="1100000000001"
          />
        </div>
      )}

      <div className="grid grid-cols-2 gap-4">
        <div className="space-y-2">
          <Label>คำนำหน้า</Label>
          <Select value={v.title} onValueChange={(t) => set({ title: t })}>
            <SelectTrigger>
              <SelectValue placeholder="คำนำหน้า" />
            </SelectTrigger>
            <SelectContent>
              {TITLE_OPTIONS.map((t) => (
                <SelectItem key={t} value={t}>
                  {t}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        <div className="space-y-2">
          <Label htmlFor="birthDate">วันเกิด</Label>
          <Input id="birthDate" type="date" value={v.birthDate} onChange={(e) => set({ birthDate: e.target.value })} />
        </div>
      </div>

      <div className="grid grid-cols-2 gap-4">
        <div className="space-y-2">
          <Label htmlFor="firstName" required>ชื่อ</Label>
          <Input id="firstName" value={v.firstName} onChange={(e) => set({ firstName: e.target.value })} placeholder="สมชาย" />
        </div>
        <div className="space-y-2">
          <Label htmlFor="lastName" required>นามสกุล</Label>
          <Input id="lastName" value={v.lastName} onChange={(e) => set({ lastName: e.target.value })} placeholder="ใจดี" />
        </div>
      </div>

      <div className="grid grid-cols-2 gap-4">
        <div className="space-y-2">
          <Label htmlFor="phone">โทรศัพท์</Label>
          <Input id="phone" value={v.phone} onChange={(e) => set({ phone: e.target.value })} />
        </div>
        <div className="space-y-2">
          <Label htmlFor="email">อีเมล</Label>
          <Input id="email" type="email" value={v.email} onChange={(e) => set({ email: e.target.value })} />
        </div>
      </div>

      <div className="space-y-3 rounded-md border p-3">
        <Label>ที่อยู่</Label>
        <Input
          value={v.addressLine}
          onChange={(e) => set({ addressLine: e.target.value })}
          placeholder="บ้านเลขที่ / หมู่ / ถนน"
        />
        <AddressSelect value={v.address} onChange={(address) => set({ address })} />
      </div>

      <div className="flex justify-end gap-2 pt-2">
        <Button variant="outline" asChild>
          <Link href="/customers">ยกเลิก</Link>
        </Button>
        <Button onClick={() => onSubmit(v)} disabled={submitting || !valid}>
          {submitting ? 'กำลังบันทึก…' : 'บันทึก'}
        </Button>
      </div>
    </div>
  );
}
