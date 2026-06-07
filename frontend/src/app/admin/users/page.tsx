'use client';

import { useState } from 'react';
import { toast } from 'sonner';
import { Plus, Pencil, Trash2, KeyRound, Users2, Check } from 'lucide-react';
import {
  useGetUsersQuery,
  useGetRolesQuery,
  useCreateUserMutation,
  useUpdateUserMutation,
  useResetUserPasswordMutation,
  useDeleteUserMutation,
  type UserAccountDto,
} from '@/lib/api/insuranceApi';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from '@/components/ui/table';
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
import { Skeleton } from '@/components/ui/skeleton';
import { Can } from '@/components/can';
import { P } from '@/lib/auth/permissions';
import { apiError, cn, fmtDateTime } from '@/lib/utils';

interface FormState {
  id: number | null;
  username: string;
  email: string;
  fullName: string;
  password: string;
  isActive: boolean;
  roleIds: number[];
}

const emptyForm = (): FormState => ({
  id: null,
  username: '',
  email: '',
  fullName: '',
  password: '',
  isActive: true,
  roleIds: [],
});

export default function UsersPage() {
  const { data: users, isLoading } = useGetUsersQuery();
  const { data: roles } = useGetRolesQuery();
  const [createUser] = useCreateUserMutation();
  const [updateUser] = useUpdateUserMutation();
  const [resetPassword] = useResetUserPasswordMutation();
  const [deleteUser] = useDeleteUserMutation();

  const [form, setForm] = useState<FormState | null>(null);
  const [resetTarget, setResetTarget] = useState<{ user: UserAccountDto; password: string } | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<UserAccountDto | null>(null);
  const [busy, setBusy] = useState(false);

  const editing = !!form && form.id !== null;
  const toggleRole = (id: number) =>
    setForm((f) => (f ? { ...f, roleIds: f.roleIds.includes(id) ? f.roleIds.filter((x) => x !== id) : [...f.roleIds, id] } : f));

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
    if (form.id === null) {
      run(
        () =>
          createUser({
            username: form.username,
            email: form.email,
            fullName: form.fullName,
            password: form.password,
            roleIds: form.roleIds,
          }).unwrap(),
        'เพิ่มผู้ใช้แล้ว',
        () => setForm(null),
      );
    } else {
      run(
        () =>
          updateUser({
            id: form.id!,
            email: form.email,
            fullName: form.fullName,
            isActive: form.isActive,
            roleIds: form.roleIds,
          }).unwrap(),
        'บันทึกแล้ว',
        () => setForm(null),
      );
    }
  };

  const formInvalid =
    !!form &&
    (form.fullName.trim() === '' ||
      form.email.trim() === '' ||
      form.roleIds.length === 0 ||
      (form.id === null && (form.username.trim().length < 3 || form.password.length < 6)));

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between gap-4">
        <div className="flex items-center gap-3">
          <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-primary/10 text-primary">
            <Users2 className="h-5 w-5" />
          </span>
          <div>
            <h1 className="text-2xl font-semibold tracking-tight">ผู้ใช้งานระบบ</h1>
            <p className="text-sm text-muted-foreground">จัดการบัญชีผู้ใช้และบทบาทการเข้าถึง</p>
          </div>
        </div>
        <Can permission={P.UserManage}>
          <Button onClick={() => setForm(emptyForm())}>
            <Plus /> เพิ่มผู้ใช้
          </Button>
        </Can>
      </div>

      <Card>
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>ชื่อผู้ใช้</TableHead>
              <TableHead>ชื่อ-นามสกุล</TableHead>
              <TableHead>อีเมล</TableHead>
              <TableHead>บทบาท</TableHead>
              <TableHead className="text-center">สถานะ</TableHead>
              <TableHead>เข้าระบบล่าสุด</TableHead>
              <TableHead className="text-right">จัดการ</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {isLoading &&
              Array.from({ length: 4 }).map((_, i) => (
                <TableRow key={i}>
                  <TableCell colSpan={7}>
                    <Skeleton className="h-5 w-full" />
                  </TableCell>
                </TableRow>
              ))}
            {users?.map((u) => (
              <TableRow key={u.id}>
                <TableCell className="font-medium">{u.username}</TableCell>
                <TableCell>{u.fullName}</TableCell>
                <TableCell className="text-muted-foreground">{u.email}</TableCell>
                <TableCell>
                  <div className="flex flex-wrap gap-1">
                    {u.roles.map((r) => (
                      <span key={r} className="rounded-full bg-slate-100 px-2 py-0.5 text-xs text-slate-700">
                        {r}
                      </span>
                    ))}
                  </div>
                </TableCell>
                <TableCell className="text-center">
                  <span
                    className={cn(
                      'inline-block rounded-full px-2 py-0.5 text-xs font-medium',
                      u.isActive ? 'bg-emerald-100 text-emerald-700' : 'bg-slate-100 text-slate-500',
                    )}
                  >
                    {u.isActive ? 'ใช้งาน' : 'ปิดใช้งาน'}
                  </span>
                </TableCell>
                <TableCell className="text-muted-foreground">{fmtDateTime(u.lastLoginAt)}</TableCell>
                <TableCell className="text-right">
                  <Can permission={P.UserManage}>
                    <div className="flex justify-end gap-1">
                      <Button
                        size="sm"
                        variant="ghost"
                        aria-label="แก้ไข"
                        onClick={() =>
                          setForm({
                            id: u.id,
                            username: u.username,
                            email: u.email,
                            fullName: u.fullName,
                            password: '',
                            isActive: u.isActive,
                            roleIds: u.roleIds,
                          })
                        }
                      >
                        <Pencil />
                      </Button>
                      <Button
                        size="sm"
                        variant="ghost"
                        aria-label="ตั้งรหัสผ่านใหม่"
                        onClick={() => setResetTarget({ user: u, password: '' })}
                      >
                        <KeyRound />
                      </Button>
                      <Button size="sm" variant="ghost" aria-label="ลบ" onClick={() => setDeleteTarget(u)}>
                        <Trash2 className="text-destructive" />
                      </Button>
                    </div>
                  </Can>
                </TableCell>
              </TableRow>
            ))}
            {!isLoading && (users?.length ?? 0) === 0 && (
              <TableRow>
                <TableCell colSpan={7} className="py-8 text-center text-muted-foreground">
                  ยังไม่มีผู้ใช้งาน
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </Card>

      {/* Add / Edit */}
      <Dialog open={!!form} onOpenChange={(o) => !o && setForm(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{editing ? 'แก้ไขผู้ใช้' : 'เพิ่มผู้ใช้'}</DialogTitle>
          </DialogHeader>
          {form && (
            <div className="space-y-4">
              {!editing && (
                <div className="space-y-2">
                  <Label required>ชื่อผู้ใช้</Label>
                  <Input value={form.username} onChange={(e) => setForm({ ...form, username: e.target.value })} placeholder="username" />
                </div>
              )}
              <div className="space-y-2">
                <Label required>ชื่อ-นามสกุล</Label>
                <Input value={form.fullName} onChange={(e) => setForm({ ...form, fullName: e.target.value })} />
              </div>
              <div className="space-y-2">
                <Label required>อีเมล</Label>
                <Input type="email" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} />
              </div>
              {!editing && (
                <div className="space-y-2">
                  <Label required>รหัสผ่าน (อย่างน้อย 6 ตัว)</Label>
                  <Input type="password" value={form.password} onChange={(e) => setForm({ ...form, password: e.target.value })} />
                </div>
              )}
              <div className="space-y-2">
                <Label required>บทบาท</Label>
                <div className="grid gap-2 sm:grid-cols-2">
                  {roles?.map((r) => {
                    const on = form.roleIds.includes(r.id);
                    return (
                      <button
                        key={r.id}
                        type="button"
                        onClick={() => toggleRole(r.id)}
                        className={cn(
                          'flex items-center gap-2 rounded-md border px-3 py-2 text-left text-sm transition-colors',
                          on ? 'border-primary bg-primary/5' : 'hover:bg-muted',
                        )}
                      >
                        <span
                          className={cn(
                            'flex h-4 w-4 items-center justify-center rounded border',
                            on ? 'border-primary bg-primary text-primary-foreground' : 'border-muted-foreground/40',
                          )}
                        >
                          {on && <Check className="h-3 w-3" />}
                        </span>
                        {r.nameTh}
                      </button>
                    );
                  })}
                </div>
              </div>
              {editing && (
                <label className="flex items-center gap-2 text-sm">
                  <input
                    type="checkbox"
                    checked={form.isActive}
                    onChange={(e) => setForm({ ...form, isActive: e.target.checked })}
                  />
                  เปิดใช้งานบัญชีนี้
                </label>
              )}
            </div>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setForm(null)}>
              ยกเลิก
            </Button>
            <Button disabled={busy || formInvalid} onClick={submitForm}>
              {busy ? 'กำลังบันทึก…' : 'บันทึก'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Reset password */}
      <Dialog open={!!resetTarget} onOpenChange={(o) => !o && setResetTarget(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>ตั้งรหัสผ่านใหม่</DialogTitle>
            <DialogDescription>
              ตั้งรหัสผ่านใหม่สำหรับ “{resetTarget?.user.username}” — เซสชันเดิมจะถูกยกเลิก
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-2">
            <Label required>รหัสผ่านใหม่ (อย่างน้อย 6 ตัว)</Label>
            <Input
              type="password"
              value={resetTarget?.password ?? ''}
              onChange={(e) => setResetTarget((t) => (t ? { ...t, password: e.target.value } : t))}
            />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setResetTarget(null)}>
              ยกเลิก
            </Button>
            <Button
              disabled={busy || (resetTarget?.password.length ?? 0) < 6}
              onClick={() =>
                resetTarget &&
                run(
                  () => resetPassword({ id: resetTarget.user.id, password: resetTarget.password }).unwrap(),
                  'ตั้งรหัสผ่านใหม่แล้ว',
                  () => setResetTarget(null),
                )
              }
            >
              {busy ? 'กำลังบันทึก…' : 'บันทึก'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Delete confirm */}
      <Dialog open={!!deleteTarget} onOpenChange={(o) => !o && setDeleteTarget(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>ลบผู้ใช้</DialogTitle>
            <DialogDescription>
              ต้องการลบผู้ใช้ “{deleteTarget?.username}” ใช่หรือไม่? การลบไม่สามารถย้อนกลับได้
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDeleteTarget(null)}>
              ยกเลิก
            </Button>
            <Button
              variant="destructive"
              disabled={busy}
              onClick={() =>
                deleteTarget &&
                run(() => deleteUser(deleteTarget.id).unwrap(), 'ลบแล้ว', () => setDeleteTarget(null))
              }
            >
              {busy ? 'กำลังลบ…' : 'ลบ'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
