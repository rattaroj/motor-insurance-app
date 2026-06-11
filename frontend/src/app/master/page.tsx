'use client';

import Link from 'next/link';
import { Car, UserSquare2, ShieldPlus, Wrench, SlidersHorizontal, ChevronRight, Database, type LucideIcon } from 'lucide-react';
import { Card } from '@/components/ui/card';
import { PageHeader } from '@/components/page-header';

type Section = {
  href: string;
  title: string;
  description: string;
  icon: LucideIcon;
};

const sections: Section[] = [
  {
    href: '/master/vehicles',
    title: 'ข้อมูลรถยนต์',
    description: 'ยี่ห้อ → รุ่น → รุ่นย่อย → ปี สำหรับใช้ตอนออกใบเสนอราคาและกรมธรรม์',
    icon: Car,
  },
  {
    href: '/master/titles',
    title: 'คำนำหน้าชื่อ',
    description: 'คำนำหน้าชื่อลูกค้า เช่น นาย นาง นางสาว',
    icon: UserSquare2,
  },
  {
    href: '/master/riders',
    title: 'ความคุ้มครองเสริม',
    description: 'ความคุ้มครองเสริม (rider) และเบี้ยที่แนบกับกรมธรรม์',
    icon: ShieldPlus,
  },
  {
    href: '/master/garages',
    title: 'อู่/ศูนย์ซ่อม',
    description: 'อู่และศูนย์ซ่อมที่อ้างอิงตอนรับเคลม',
    icon: Wrench,
  },
  {
    href: '/master/rates',
    title: 'พิกัดอัตราเบี้ย',
    description: 'อัตราเบี้ยฐานต่อชั้นความคุ้มครอง ปรับได้โดยไม่ต้อง deploy',
    icon: SlidersHorizontal,
  },
];

export default function MasterDataPage() {
  return (
    <div className="space-y-6">
      <PageHeader icon={Database} title="ข้อมูลหลัก" description="เลือกหมวดข้อมูลที่ต้องการจัดการ" />

      <div className="grid gap-4 sm:grid-cols-2">
        {sections.map(({ href, title, description, icon: Icon }) => (
          <Link key={href} href={href} className="group">
            <Card className="flex items-start gap-4 p-5 transition-colors hover:border-primary/40 hover:bg-muted/40">
              <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-lg bg-primary/10 text-primary">
                <Icon className="h-5 w-5" />
              </span>
              <div className="min-w-0 flex-1">
                <p className="font-medium">{title}</p>
                <p className="mt-0.5 text-sm text-muted-foreground">{description}</p>
              </div>
              <ChevronRight className="mt-1 h-5 w-5 shrink-0 text-muted-foreground transition-transform group-hover:translate-x-0.5 group-hover:text-foreground" />
            </Card>
          </Link>
        ))}
      </div>
    </div>
  );
}
