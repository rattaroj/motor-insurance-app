'use client';

import { useMemo, useState } from 'react';
import { toast } from 'sonner';
import { Plus, Pencil, Trash2, type LucideIcon } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { DataTable, type Column } from '@/components/data-table';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Can } from '@/components/can';
import { PageHeader } from '@/components/page-header';
import { apiError } from '@/lib/utils';
import { useDebouncedValue } from '@/lib/use-debounced';

const PAGE_SIZE = 10;

/** One field rendered in the add/edit dialog. */
export interface MasterField {
  name: string;
  label: string;
  type?: 'text' | 'number';
  /** Defaults to true. Optional fields never block save. */
  required?: boolean;
  placeholder?: string;
}

export type MasterValues = Record<string, string>;

interface MasterTableProps<T> {
  title: string;
  description?: string;
  icon?: LucideIcon;
  rows: T[] | undefined;
  loading: boolean;
  getId: (row: T) => number;
  /** Human label of a row, used in the delete confirm + default search text. */
  getName: (row: T) => string;
  /** Display columns (the "จัดการ" actions column is appended automatically). */
  columns: Column<T>[];
  /** Text matched against the search box. Defaults to `getName`. */
  searchText?: (row: T) => string;
  searchPlaceholder?: string;
  /** Form fields shown in the add/edit dialog. */
  fields: MasterField[];
  /** Prefill the edit dialog from a row. */
  toValues: (row: T) => MasterValues;
  onCreate: (values: MasterValues) => Promise<unknown>;
  onUpdate: (id: number, values: MasterValues) => Promise<unknown>;
  onDelete: (id: number) => Promise<unknown>;
  /** Permission gating the add/edit/delete controls (mutations are enforced server-side too). */
  permission: string;
  /** e.g. "เพิ่มยี่ห้อ" */
  addLabel: string;
  emptyText?: string;
}

const isFilled = (v: string | undefined) => (v ?? '').trim() !== '';

/**
 * Generic master-data table: client-side search + pagination over a small lookup list,
 * with add/edit/delete dialogs driven by a `fields` config. Used by the master sub-pages
 * (คำนำหน้าชื่อ, ความคุ้มครองเสริม, อู่/ศูนย์ซ่อม) so each page only declares its columns + fields.
 */
export function MasterTable<T>({
  title,
  description,
  icon: Icon,
  rows,
  loading,
  getId,
  getName,
  columns,
  searchText,
  searchPlaceholder = 'ค้นหา…',
  fields,
  toValues,
  onCreate,
  onUpdate,
  onDelete,
  permission,
  addLabel,
  emptyText = 'ยังไม่มีข้อมูล',
}: MasterTableProps<T>) {
  const [page, setPage] = useState(1);
  const [searchInput, setSearchInput] = useState('');
  const search = useDebouncedValue(searchInput, 300);

  // form state: null = closed, { id: null } = add, { id: number } = edit
  const [form, setForm] = useState<{ id: number | null; values: MasterValues } | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<T | null>(null);
  const [busy, setBusy] = useState(false);

  const toText = searchText ?? getName;
  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    const all = rows ?? [];
    return q ? all.filter((r) => toText(r).toLowerCase().includes(q)) : all;
  }, [rows, search, toText]);

  const pageRows = filtered.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);

  const emptyValues = (): MasterValues => Object.fromEntries(fields.map((f) => [f.name, '']));
  const missingRequired =
    !!form && fields.some((f) => f.required !== false && !isFilled(form.values[f.name]));

  const run = async (fn: () => Promise<unknown>, ok: string, done: () => void) => {
    setBusy(true);
    try {
      await fn();
      toast.success(ok);
      done();
    } catch (e) {
      toast.error(apiError(e));
    } finally {
      setBusy(false);
    }
  };

  const submitForm = () => {
    if (!form) return;
    const { id, values } = form;
    if (id === null) {
      run(() => onCreate(values), 'เพิ่มแล้ว', () => setForm(null));
    } else {
      run(() => onUpdate(id, values), 'บันทึกแล้ว', () => setForm(null));
    }
  };

  const submitDelete = () => {
    if (!deleteTarget) return;
    run(() => onDelete(getId(deleteTarget)), 'ลบแล้ว', () => setDeleteTarget(null));
  };

  const allColumns: Column<T>[] = [
    ...columns,
    {
      header: 'จัดการ',
      className: 'text-right w-[1%] whitespace-nowrap',
      cell: (row) => (
        <Can permission={permission}>
          <div className="flex justify-end gap-1">
            <Button
              size="sm"
              variant="ghost"
              aria-label="แก้ไข"
              onClick={() => setForm({ id: getId(row), values: toValues(row) })}
            >
              <Pencil />
            </Button>
            <Button size="sm" variant="ghost" aria-label="ลบ" onClick={() => setDeleteTarget(row)}>
              <Trash2 className="text-destructive" />
            </Button>
          </div>
        </Can>
      ),
    },
  ];

  return (
    <div className="space-y-6">
      <PageHeader
        icon={Icon}
        title={title}
        description={description}
        actions={
          <Can permission={permission}>
            <Button onClick={() => setForm({ id: null, values: emptyValues() })}>
              <Plus /> {addLabel}
            </Button>
          </Can>
        }
      />

      <DataTable<T>
        rows={pageRows}
        loading={loading}
        getKey={getId}
        columns={allColumns}
        page={page}
        pageSize={PAGE_SIZE}
        totalCount={filtered.length}
        onPageChange={setPage}
        search={searchInput}
        onSearchChange={(v) => {
          setSearchInput(v);
          setPage(1);
        }}
        searchPlaceholder={searchPlaceholder}
        emptyText={emptyText}
      />

      {/* Add / Edit */}
      <Dialog open={!!form} onOpenChange={(o) => !o && setForm(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              {form?.id === null ? 'เพิ่ม' : 'แก้ไข'}
              {title}
            </DialogTitle>
          </DialogHeader>
          <div className="space-y-4">
            {fields.map((f) => (
              <div key={f.name} className="space-y-2">
                <Label htmlFor={`mf-${f.name}`} required={f.required !== false}>
                  {f.label}
                </Label>
                <Input
                  id={`mf-${f.name}`}
                  type={f.type ?? 'text'}
                  placeholder={f.placeholder}
                  value={form?.values[f.name] ?? ''}
                  onChange={(e) =>
                    setForm((prev) =>
                      prev ? { ...prev, values: { ...prev.values, [f.name]: e.target.value } } : prev,
                    )
                  }
                />
              </div>
            ))}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setForm(null)}>
              ยกเลิก
            </Button>
            <Button disabled={busy || missingRequired} onClick={submitForm}>
              {busy ? 'กำลังบันทึก…' : 'บันทึก'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Delete confirm */}
      <Dialog open={!!deleteTarget} onOpenChange={(o) => !o && setDeleteTarget(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>ลบ{title}</DialogTitle>
            <DialogDescription>
              ต้องการลบ “{deleteTarget ? getName(deleteTarget) : ''}” ใช่หรือไม่? การลบไม่สามารถย้อนกลับได้
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDeleteTarget(null)}>
              ยกเลิก
            </Button>
            <Button variant="destructive" disabled={busy} onClick={submitDelete}>
              {busy ? 'กำลังลบ…' : 'ลบ'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
