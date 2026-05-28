import type { Metadata } from 'next';
import './globals.css';
import { StoreProvider } from '@/lib/store/provider';
import { AppShell } from '@/components/app-shell';
import { Toaster } from '@/components/ui/sonner';

export const metadata: Metadata = {
  title: 'Motor Insurance',
  description: 'ระบบจัดการประกันรถยนต์',
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="th">
      <body>
        <StoreProvider>
          <AppShell>{children}</AppShell>
          <Toaster />
        </StoreProvider>
      </body>
    </html>
  );
}
