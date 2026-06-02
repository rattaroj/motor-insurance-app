'use client';

import { useState } from 'react';
import Lightbox from 'yet-another-react-lightbox';
import Zoom from 'yet-another-react-lightbox/plugins/zoom';
import Thumbnails from 'yet-another-react-lightbox/plugins/thumbnails';
import 'yet-another-react-lightbox/styles.css';
import 'yet-another-react-lightbox/plugins/thumbnails.css';
import { ZoomIn } from 'lucide-react';
import { cn } from '@/lib/utils';

/** A square-ish image thumbnail with a hover zoom overlay. */
function Thumb({ src, alt, className }: { src: string; alt: string; className?: string }) {
  return (
    <span
      className={cn(
        'group relative block aspect-[3/2] w-20 shrink-0 overflow-hidden rounded-md border bg-muted',
        className,
      )}
    >
      {/* eslint-disable-next-line @next/next/no-img-element */}
      <img src={src} alt={alt} className="h-full w-full object-cover transition duration-200 group-hover:scale-105" />
      <span className="absolute inset-0 flex items-center justify-center bg-slate-900/0 opacity-0 transition duration-200 group-hover:bg-slate-900/40 group-hover:opacity-100">
        <ZoomIn className="h-5 w-5 text-white" />
      </span>
    </span>
  );
}

interface ImagePreviewProps {
  src: string;
  alt: string;
  className?: string;
}

/** Single image: thumbnail → modern lightbox with pinch/scroll zoom. */
export function ImagePreview({ src, alt, className }: ImagePreviewProps) {
  const [open, setOpen] = useState(false);

  return (
    <>
      <button
        type="button"
        onClick={() => setOpen(true)}
        className="rounded-md ring-offset-background focus:outline-none focus:ring-2 focus:ring-ring focus:ring-offset-2"
        aria-label={`ดูรูป ${alt}`}
      >
        <Thumb src={src} alt={alt} className={className} />
      </button>

      <Lightbox
        open={open}
        close={() => setOpen(false)}
        slides={[{ src, alt }]}
        plugins={[Zoom]}
        carousel={{ finite: true }}
        render={{ buttonPrev: () => null, buttonNext: () => null }}
        styles={{ container: { backgroundColor: 'rgba(15, 23, 42, 0.92)' } }}
      />
    </>
  );
}

export interface GalleryItem {
  src: string;
  alt: string;
  title: string;
  subtitle?: string;
}

/**
 * A grid of labelled image cards sharing one lightbox — clicking any card opens
 * the gallery at that image with zoom + a thumbnail strip to browse the rest.
 */
export function ImageGallery({ items }: { items: GalleryItem[] }) {
  const [index, setIndex] = useState(-1);

  return (
    <>
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
        {items.map((it, i) => (
          <button
            key={i}
            type="button"
            onClick={() => setIndex(i)}
            className="flex items-center gap-3 rounded-md border p-3 text-left ring-offset-background transition hover:bg-muted/50 focus:outline-none focus:ring-2 focus:ring-ring focus:ring-offset-2"
            aria-label={`ดูรูป ${it.alt}`}
          >
            <Thumb src={it.src} alt={it.alt} />
            <div className="min-w-0">
              <p className="truncate font-medium">{it.title}</p>
              {it.subtitle && <p className="text-xs tabular-nums text-muted-foreground">{it.subtitle}</p>}
            </div>
          </button>
        ))}
      </div>

      <Lightbox
        open={index >= 0}
        close={() => setIndex(-1)}
        index={index < 0 ? 0 : index}
        slides={items.map((it) => ({ src: it.src, alt: it.alt, title: it.title }))}
        plugins={items.length > 1 ? [Zoom, Thumbnails] : [Zoom]}
        carousel={{ finite: true }}
        styles={{ container: { backgroundColor: 'rgba(15, 23, 42, 0.92)' } }}
      />
    </>
  );
}
