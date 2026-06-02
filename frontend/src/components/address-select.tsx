'use client';

import * as React from 'react';
import {
  useGetProvincesQuery,
  useGetDistrictsQuery,
  useGetSubdistrictsQuery,
} from '@/lib/api/insuranceApi';
import { Combobox, type ComboboxOption } from '@/components/ui/combobox';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';

/** The address selection held by the parent form. `postalCode` is the display text (auto-filled). */
export interface AddressValue {
  provinceId: number | null;
  districtId: number | null;
  subdistrictId: number | null;
  postalCodeId: number | null;
  postalCode: string | null;
}

export const emptyAddress: AddressValue = {
  provinceId: null,
  districtId: null,
  subdistrictId: null,
  postalCodeId: null,
  postalCode: null,
};

interface AddressSelectProps {
  value: AddressValue;
  onChange: (next: AddressValue) => void;
  disabled?: boolean;
}

interface ProvinceSelectProps {
  /** The selected province name (Thai). Stored as a plain string (e.g. a vehicle's plate province). */
  value: string;
  onChange: (provinceName: string) => void;
  disabled?: boolean;
  id?: string;
}

/**
 * Standalone searchable province picker that reads/writes the province *name* (string),
 * for fields that only need a province and store it as text (e.g. a vehicle's registration
 * province) rather than the full cascading address.
 */
export function ProvinceSelect({ value, onChange, disabled, id }: ProvinceSelectProps) {
  const { data: provinces, isFetching } = useGetProvincesQuery();
  const options: ComboboxOption[] = React.useMemo(
    () => (provinces ?? []).map((p) => ({ value: p.id, label: p.nameTh, sublabel: p.nameEn })),
    [provinces],
  );
  const selectedId = React.useMemo(
    () => (provinces ?? []).find((p) => p.nameTh === value)?.id ?? null,
    [provinces, value],
  );

  return (
    <Combobox
      id={id}
      value={selectedId}
      onChange={(pid) => onChange((provinces ?? []).find((p) => p.id === pid)?.nameTh ?? '')}
      options={options}
      loading={isFetching}
      disabled={disabled}
      placeholder="เลือกจังหวัด"
      searchPlaceholder="ค้นหาจังหวัด…"
    />
  );
}

/**
 * Cascading Thai-address picker: province → district → subdistrict, each pulled from the
 * geography master via RTK Query. Selecting a parent resets its children; selecting a
 * subdistrict auto-fills the (read-only) postal code. Each level is a searchable Combobox.
 */
export function AddressSelect({ value, onChange, disabled }: AddressSelectProps) {
  const { data: provinces, isFetching: loadingProvinces } = useGetProvincesQuery();
  const { data: districts, isFetching: loadingDistricts } = useGetDistrictsQuery(value.provinceId!, {
    skip: !value.provinceId,
  });
  const { data: subdistricts, isFetching: loadingSubdistricts } = useGetSubdistrictsQuery(value.districtId!, {
    skip: !value.districtId,
  });

  const provinceOptions: ComboboxOption[] = React.useMemo(
    () => (provinces ?? []).map((p) => ({ value: p.id, label: p.nameTh, sublabel: p.nameEn })),
    [provinces],
  );
  const districtOptions: ComboboxOption[] = React.useMemo(
    () => (districts ?? []).map((d) => ({ value: d.id, label: d.nameTh, sublabel: d.nameEn })),
    [districts],
  );
  const subdistrictOptions: ComboboxOption[] = React.useMemo(
    () =>
      (subdistricts ?? []).map((s) => ({
        value: s.id,
        label: s.nameTh,
        sublabel: `${s.nameEn} · ${s.postalCode}`,
      })),
    [subdistricts],
  );

  const setProvince = (provinceId: number | null) =>
    onChange({ ...emptyAddress, provinceId });

  const setDistrict = (districtId: number | null) =>
    onChange({ ...value, districtId, subdistrictId: null, postalCodeId: null, postalCode: null });

  const setSubdistrict = (subdistrictId: number | null) => {
    const opt = (subdistricts ?? []).find((s) => s.id === subdistrictId);
    onChange({
      ...value,
      subdistrictId,
      postalCodeId: opt?.postalCodeId ?? null,
      postalCode: opt?.postalCode ?? null,
    });
  };

  return (
    <div className="grid grid-cols-2 gap-4">
      <div className="space-y-2">
        <Label>จังหวัด</Label>
        <Combobox
          value={value.provinceId}
          onChange={setProvince}
          options={provinceOptions}
          loading={loadingProvinces}
          disabled={disabled}
          placeholder="เลือกจังหวัด"
          searchPlaceholder="ค้นหาจังหวัด…"
        />
      </div>

      <div className="space-y-2">
        <Label>อำเภอ / เขต</Label>
        <Combobox
          value={value.districtId}
          onChange={setDistrict}
          options={districtOptions}
          loading={loadingDistricts}
          disabled={disabled || !value.provinceId}
          placeholder={value.provinceId ? 'เลือกอำเภอ' : 'เลือกจังหวัดก่อน'}
          searchPlaceholder="ค้นหาอำเภอ…"
        />
      </div>

      <div className="space-y-2">
        <Label>ตำบล / แขวง</Label>
        <Combobox
          value={value.subdistrictId}
          onChange={setSubdistrict}
          options={subdistrictOptions}
          loading={loadingSubdistricts}
          disabled={disabled || !value.districtId}
          placeholder={value.districtId ? 'เลือกตำบล' : 'เลือกอำเภอก่อน'}
          searchPlaceholder="ค้นหาตำบล…"
        />
      </div>

      <div className="space-y-2">
        <Label>รหัสไปรษณีย์</Label>
        <Input
          value={value.postalCode ?? ''}
          readOnly
          placeholder="เลือกตำบลก่อน"
          className="bg-muted/40"
        />
      </div>
    </div>
  );
}
