'use client';

import { useState } from 'react';
import { Download } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { apiError, saveUrl } from '@/lib/utils';

/**
 * Triggers a CSV export: calls `fetchUrl` (an RTK Query export mutation that resolves to an
 * object URL) and saves it as `filename`. Shared by the list/worklist pages.
 */
export function ExportButton({
  filename,
  fetchUrl,
  label = 'Export CSV',
}: {
  filename: string;
  fetchUrl: () => Promise<string>;
  label?: string;
}) {
  const [busy, setBusy] = useState(false);
  return (
    <Button
      variant="outline"
      size="sm"
      disabled={busy}
      onClick={async () => {
        setBusy(true);
        try {
          saveUrl(await fetchUrl(), filename);
        } catch (e) {
          toast.error(apiError(e));
        } finally {
          setBusy(false);
        }
      }}
    >
      <Download /> {label}
    </Button>
  );
}
