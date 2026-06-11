import type { LucideIcon } from 'lucide-react';
import type { ReactNode } from 'react';
import { cn } from '@/lib/utils';

interface PageHeaderProps {
  title: ReactNode;
  /** Rendered inline next to the title (e.g. a StatusBadge). */
  badge?: ReactNode;
  description?: ReactNode;
  icon?: LucideIcon;
  /** Right-aligned slot for buttons / filters. */
  actions?: ReactNode;
  className?: string;
}

/** Highlighted page heading used at the top of every page. */
export function PageHeader({ title, badge, description, icon: Icon, actions, className }: PageHeaderProps) {
  return (
    <div
      className={cn(
        'relative overflow-hidden rounded-xl border border-primary/15 bg-gradient-to-r from-primary/10 via-primary/5 to-transparent px-5 py-4 shadow-sm',
        className,
      )}
    >
      <div aria-hidden className="absolute inset-y-0 left-0 w-1 bg-gradient-to-b from-sidebar to-primary" />
      <div className="flex flex-wrap items-center justify-between gap-x-4 gap-y-3">
        <div className="flex items-center gap-3">
          {Icon && (
            <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-gradient-to-br from-sidebar to-primary text-white shadow-sm">
              <Icon className="h-5 w-5" />
            </span>
          )}
          <div>
            <div className="flex flex-wrap items-center gap-3">
              <h1 className="bg-gradient-to-r from-sidebar to-primary bg-clip-text text-2xl font-bold tracking-tight text-transparent dark:from-sky-300 dark:to-blue-400">
                {title}
              </h1>
              {badge}
            </div>
            {description && <div className="mt-0.5 text-sm text-muted-foreground">{description}</div>}
          </div>
        </div>
        {actions && <div className="flex flex-wrap items-center gap-2">{actions}</div>}
      </div>
    </div>
  );
}
