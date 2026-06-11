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
    <html lang="th" suppressHydrationWarning>
      <head>
        {/* Apply the saved theme before paint to avoid a light-mode flash. */}
        <script
          dangerouslySetInnerHTML={{
            __html: `try{if(localStorage.getItem('theme')==='dark')document.documentElement.classList.add('dark')}catch(e){}`,
          }}
        />
      </head>
      <body>
        <StoreProvider>
          <AppShell>{children}</AppShell>
          <Toaster />
        </StoreProvider>
      </body>
    </html>
  );
}
