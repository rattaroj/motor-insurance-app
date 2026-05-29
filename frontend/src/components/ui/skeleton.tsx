import { cn } from '@/lib/utils';

/** Pulsing placeholder block. Compose with width/height utilities, e.g. <Skeleton className="h-4 w-24" />. */
function Skeleton({ className, ...props }: React.HTMLAttributes<HTMLDivElement>) {
  return <div className={cn('animate-pulse rounded-md bg-muted', className)} {...props} />;
}

export { Skeleton };
