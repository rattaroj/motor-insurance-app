'use client';

import { useState } from 'react';
import {
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
  useGetCustomerTitlesQuery,
  useCreateCustomerTitleMutation,
  useUpdateCustomerTitleMutation,
  useDeleteCustomerTitleMutation,
  useGetRidersQuery,
  useCreateRiderMutation,
  useUpdateRiderMutation,
  useDeleteRiderMutation,
  POWERTRAIN_LABELS,
  POWERTRAIN_OPTIONS,
  type Powertrain,
} from '@/lib/api/insuranceApi';
import { MasterColumn, type MasterItem } from '@/components/master-column';
import { fmtBaht } from '@/lib/utils';

export default function MasterDataPage() {
  const [brandId, setBrandId] = useState<number | null>(null);
  const [modelId, setModelId] = useState<number | null>(null);
  const [submodelId, setSubmodelId] = useState<number | null>(null);

  const { data: brands } = useGetVehicleBrandsQuery();
  const { data: models } = useGetVehicleModelsQuery(brandId!, { skip: !brandId });
  const { data: submodels } = useGetVehicleSubmodelsQuery(modelId!, { skip: !modelId });
  const { data: years } = useGetVehicleModelYearsQuery(submodelId!, { skip: !submodelId });

  const [createBrand] = useCreateBrandMutation();
  const [updateBrand] = useUpdateBrandMutation();
  const [deleteBrand] = useDeleteBrandMutation();
  const [createModel] = useCreateModelMutation();
  const [updateModel] = useUpdateModelMutation();
  const [deleteModel] = useDeleteModelMutation();
  const [createSubmodel] = useCreateSubmodelMutation();
  const [updateSubmodel] = useUpdateSubmodelMutation();
  const [deleteSubmodel] = useDeleteSubmodelMutation();
  const [createModelYear] = useCreateModelYearMutation();
  const [updateModelYear] = useUpdateModelYearMutation();
  const [deleteModelYear] = useDeleteModelYearMutation();

  const { data: titles } = useGetCustomerTitlesQuery();
  const [createTitle] = useCreateCustomerTitleMutation();
  const [updateTitle] = useUpdateCustomerTitleMutation();
  const [deleteTitle] = useDeleteCustomerTitleMutation();

  const { data: riders } = useGetRidersQuery();
  const [createRider] = useCreateRiderMutation();
  const [updateRider] = useUpdateRiderMutation();
  const [deleteRider] = useDeleteRiderMutation();

  const toItems = (xs?: { id: number; name: string }[]): MasterItem[] =>
    (xs ?? []).map((x) => ({ id: x.id, label: x.name }));
  const submodelItems: MasterItem[] = (submodels ?? []).map((s) => ({
    id: s.id,
    label: s.name,
    meta: POWERTRAIN_LABELS[s.powertrain],
    selectValue: s.powertrain,
  }));
  const yearItems: MasterItem[] = (years ?? []).map((y) => ({ id: y.id, label: String(y.year) }));

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">ข้อมูลหลักรถยนต์</h1>
        <p className="text-sm text-muted-foreground">จัดการ ยี่ห้อ → รุ่น → รุ่นย่อย → ปี (เลือกเพื่อดูระดับถัดไป)</p>
      </div>

      <div className="grid gap-4 lg:grid-cols-4">
        <MasterColumn
          title="ยี่ห้อ"
          fieldLabel="ชื่อยี่ห้อ"
          items={toItems(brands)}
          selectedId={brandId}
          onSelect={(id) => {
            setBrandId(id);
            setModelId(null);
            setSubmodelId(null);
          }}
          onAdd={(v) => createBrand({ name: v }).unwrap()}
          onEdit={(id, v) => updateBrand({ id, name: v }).unwrap()}
          onDelete={async (id) => {
            await deleteBrand(id).unwrap();
            if (brandId === id) {
              setBrandId(null);
              setModelId(null);
              setSubmodelId(null);
            }
          }}
        />

        <MasterColumn
          title="รุ่น"
          fieldLabel="ชื่อรุ่น"
          items={toItems(models)}
          selectedId={modelId}
          disabled={!brandId}
          disabledHint="เลือกยี่ห้อก่อน"
          onSelect={(id) => {
            setModelId(id);
            setSubmodelId(null);
          }}
          onAdd={(v) => createModel({ brandId: brandId!, name: v }).unwrap()}
          onEdit={(id, v) => updateModel({ id, name: v }).unwrap()}
          onDelete={async (id) => {
            await deleteModel(id).unwrap();
            if (modelId === id) {
              setModelId(null);
              setSubmodelId(null);
            }
          }}
        />

        <MasterColumn
          title="รุ่นย่อย"
          fieldLabel="ชื่อรุ่นย่อย"
          items={submodelItems}
          selectedId={submodelId}
          disabled={!modelId}
          disabledHint="เลือกรุ่นก่อน"
          selectField={{ label: 'ประเภทพลังงาน', options: POWERTRAIN_OPTIONS }}
          onSelect={(id) => setSubmodelId(id)}
          onAdd={(v, pt) => createSubmodel({ modelId: modelId!, name: v, powertrain: pt as Powertrain }).unwrap()}
          onEdit={(id, v, pt) => updateSubmodel({ id, name: v, powertrain: pt as Powertrain }).unwrap()}
          onDelete={async (id) => {
            await deleteSubmodel(id).unwrap();
            if (submodelId === id) setSubmodelId(null);
          }}
        />

        <MasterColumn
          title="ปีรถ"
          fieldLabel="ปี (ค.ศ.)"
          inputType="number"
          items={yearItems}
          selectedId={null}
          selectable={false}
          disabled={!submodelId}
          disabledHint="เลือกรุ่นย่อยก่อน"
          onSelect={() => {}}
          onAdd={(v) => createModelYear({ submodelId: submodelId!, year: Number(v) }).unwrap()}
          onEdit={(id, v) => updateModelYear({ id, year: Number(v) }).unwrap()}
          onDelete={(id) => deleteModelYear(id).unwrap()}
        />
      </div>

      <div className="pt-2">
        <h1 className="text-2xl font-semibold tracking-tight">ข้อมูลหลักการรับประกัน</h1>
        <p className="text-sm text-muted-foreground">คำนำหน้าชื่อลูกค้า และความคุ้มครองเสริม (rider) ที่ใช้คิดเบี้ย</p>
      </div>

      <div className="grid gap-4 lg:grid-cols-4">
        <MasterColumn
          title="คำนำหน้าชื่อ"
          fieldLabel="คำนำหน้า"
          items={toItems(titles)}
          selectedId={null}
          selectable={false}
          onSelect={() => {}}
          onAdd={(v) => createTitle({ name: v }).unwrap()}
          onEdit={(id, v) => updateTitle({ id, name: v }).unwrap()}
          onDelete={(id) => deleteTitle(id).unwrap()}
        />

        <MasterColumn
          title="ความคุ้มครองเสริม"
          fieldLabel="ชื่อความคุ้มครอง"
          items={(riders ?? []).map((r) => ({
            id: r.id,
            label: r.name,
            meta: fmtBaht(r.premium),
            selectValue: String(r.premium),
          }))}
          selectedId={null}
          selectable={false}
          numberField={{ label: 'เบี้ย (บาท)', placeholder: '1000' }}
          onSelect={() => {}}
          onAdd={(v, premium) => createRider({ name: v, premium: Number(premium) }).unwrap()}
          onEdit={(id, v, premium) => updateRider({ id, name: v, premium: Number(premium) }).unwrap()}
          onDelete={(id) => deleteRider(id).unwrap()}
        />
      </div>
    </div>
  );
}
