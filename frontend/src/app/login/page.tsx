'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { ShieldCheck } from 'lucide-react';
import { useLoginMutation } from '@/lib/api/insuranceApi';
import { useAppDispatch } from '@/lib/store/store';
import { setCredentials } from '@/lib/auth/authSlice';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { apiError } from '@/lib/utils';

const DEMO_ACCOUNTS = [
    { username: 'admin', password: 'Admin@123', label: 'ผู้ดูแลระบบ (ทุกสิทธิ์)' },
    { username: 'underwriter', password: 'Under@123', label: 'เจ้าหน้าที่รับประกัน' },
    { username: 'claims', password: 'Claims@123', label: 'เจ้าหน้าที่สินไหม' },
    { username: 'finance', password: 'Finance@123', label: 'เจ้าหน้าที่การเงิน' },
    { username: 'viewer', password: 'Viewer@123', label: 'ผู้ดูข้อมูล (อ่านอย่างเดียว)' },
];

export default function LoginPage() {
    const router = useRouter();
    const dispatch = useAppDispatch();
    const [login, { isLoading }] = useLoginMutation();
    const [username, setUsername] = useState('');
    const [password, setPassword] = useState('');
    const [error, setError] = useState<string | null>(null);

    const submit = async (e: React.FormEvent) => {
        e.preventDefault();
        setError(null);
        try {
            const res = await login({ username, password }).unwrap();
            dispatch(setCredentials(res));
            router.replace('/');
        } catch (err) {
            setError(apiError(err));
        }
    };

    const fillDemo = (u: string, p: string) => {
        setUsername(u);
        setPassword(p);
    };

    return (
        <div className="flex min-h-screen items-center justify-center bg-muted/30 px-4 py-8">
            <Card className="w-full max-w-md">
                <CardHeader className="space-y-2 text-center">
                    <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-primary/10">
                        <ShieldCheck className="h-6 w-6 text-primary" />
                    </div>
                    <CardTitle className="text-2xl">เข้าสู่ระบบ</CardTitle>
                    <CardDescription>ระบบจัดการประกันรถยนต์</CardDescription>
                </CardHeader>
                <CardContent>
                    <form onSubmit={submit} className="space-y-4">
                        <div className="space-y-2">
                            <Label htmlFor="username" required>ชื่อผู้ใช้</Label>
                            <Input
                                id="username"
                                value={username}
                                onChange={(e) => setUsername(e.target.value)}
                                autoComplete="username"
                                placeholder="admin"
                                autoFocus
                            />
                        </div>
                        <div className="space-y-2">
                            <Label htmlFor="password" required>รหัสผ่าน</Label>
                            <Input
                                id="password"
                                type="password"
                                value={password}
                                onChange={(e) => setPassword(e.target.value)}
                                autoComplete="current-password"
                                placeholder="••••••••"
                            />
                        </div>

                        {error && (
                            <p className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">{error}</p>
                        )}

                        <Button type="submit" className="w-full" disabled={isLoading || !username || !password}>
                            {isLoading ? 'กำลังเข้าสู่ระบบ…' : 'เข้าสู่ระบบ'}
                        </Button>
                    </form>

                    <div className="mt-6 border-t pt-4">
                        <p className="mb-2 text-xs font-medium text-muted-foreground">บัญชีทดสอบ (คลิกเพื่อกรอกอัตโนมัติ)</p>
                        <div className="space-y-1">
                            {DEMO_ACCOUNTS.map((a) => (
                                <button
                                    key={a.username}
                                    type="button"
                                    onClick={() => fillDemo(a.username, a.password)}
                                    className="flex w-full items-center justify-between rounded-md px-2 py-1.5 text-left text-xs transition-colors hover:bg-muted"
                                >
                                    <span className="font-mono">{a.username}</span>
                                    <span className="text-muted-foreground">{a.label}</span>
                                </button>
                            ))}
                        </div>
                    </div>
                </CardContent>
            </Card>
        </div>
    );
}
