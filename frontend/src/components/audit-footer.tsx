import { Clock, PencilLine, UserCircle2 } from 'lucide-react';
import { fmtDateTime } from '@/lib/utils';
import type { AuditInfo } from '@/lib/api/insuranceApi';

/**
 * Small "created by / last updated by" line from a record's audit columns.
 * Stamped automatically by the backend (ICurrentUser); the user is null for system work.
 */
export function AuditFooter({ audit }: { audit: AuditInfo }) {
  return (
    <div className="flex flex-wrap items-center gap-x-5 gap-y-1 border-t pt-3 text-xs text-muted-foreground">
      <span className="inline-flex items-center gap-1.5">
        <UserCircle2 className="h-3.5 w-3.5" />
        สร้างโดย {audit.createdUser ?? 'ระบบ'}
        <span className="inline-flex items-center gap-1 text-muted-foreground/80">
          <Clock className="h-3 w-3" /> {fmtDateTime(audit.createdAt)}
        </span>
      </span>
      {audit.updatedAt && (
        <span className="inline-flex items-center gap-1.5">
          <PencilLine className="h-3.5 w-3.5" />
          แก้ไขล่าสุดโดย {audit.updatedUser ?? 'ระบบ'}
          <span className="inline-flex items-center gap-1 text-muted-foreground/80">
            <Clock className="h-3 w-3" /> {fmtDateTime(audit.updatedAt)}
          </span>
        </span>
      )}
    </div>
  );
}
