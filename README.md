# ระบบประกันรถยนต์ — Motor Insurance Workflow

Full-stack ครอบคลุม workflow ประกันรถยนต์: quotation → policy → claim → payment → **renewal**
พร้อมการคิดเบี้ยขั้นสูง (พิกัดอัตราปรับได้/มีเวอร์ชัน), ออกเอกสาร PDF (กรมธรรม์/ใบเสร็จ/ใบเสนอราคา/เปรียบเทียบ/จดหมายสินไหม),
ต่ออายุเชิงรุก + แจ้งเตือนหลายช่องทาง (log/SMTP/LINE) + ส่งซ้ำ + แจ้งตาม event, งานเบื้องหลังหมดอายุ/เตือนต่ออายุอัตโนมัติ,
คืนเบี้ย pro-rata, สลักหลังปรับความคุ้มครอง + เบี้ยส่วนต่าง, PromptPay QR, แดชบอร์ดงานค้าง + ไทม์ไลน์กิจกรรม,
worklist เคลม SLA, จัดการผู้ใช้/บทบาท และรายงาน/วิเคราะห์

- **Backend**: .NET 8, Clean Architecture, **FastEndpoints (REPR)**, EF Core, SQL Server, QuestPDF (PDF) + QRCoder (PromptPay), `BackgroundService` (งานหมดอายุ/เตือนต่ออายุ), แจ้งเตือน SMTP (System.Net.Mail) / LINE Notify
- **Frontend**: Next.js (App Router), RTK Query, Tailwind CSS
- **Database**: SQL Server — schema เป็นเจ้าของโดย **Liquibase** (EF = ORM อย่างเดียว ไม่ migrate)

---

## โครงสร้าง

```
backend/
  MotorInsurance.sln
  src/
    MotorInsurance.Domain/          # entities, enums, state machines (ไม่พึ่ง layer อื่น)
    MotorInsurance.Application/      # IAppDbContext, validators, helpers (PremiumCalculator/AuthFlow), interfaces
    MotorInsurance.Infrastructure/  # AppDbContext, EF configs, services (file storage, notification sender), DI
    MotorInsurance.Api/             # FastEndpoints (1 ไฟล์/endpoint), Documents (QuestPDF), middleware, Program.cs
  tests/MotorInsurance.Tests/       # state machine + handler slice tests (xUnit)
db/
  db.changelog-master.xml           # Liquibase (SQL-first + rollback) — V001..V017
  scripts/V0xx__*.sql
frontend/
  src/app/                          # App Router pages (customers/vehicles/quotations/policies/renewals/claims/payments/reports/master)
  src/lib/api/insuranceApi.ts       # RTK Query (ทุก endpoint + types)
  src/lib/store/                    # Redux store + provider
  src/components/                   # shared UI (master-column, claim-manage-dialog, promptpay-button, ฯลฯ)
```

## Dependency rule (Clean Architecture)

```
Api → Application → Domain
Infrastructure → Application → Domain
```

Application พึ่ง `IAppDbContext` (interface) ไม่ใช่ `AppDbContext` (Infrastructure) — กลับทิศ dependency ผ่าน DI

---

## รันโปรเจกต์

### 1. Database (Liquibase)

```bash
liquibase --changeLogFile=db/db.changelog-master.xml \
  --url="jdbc:sqlserver://localhost:1433;databaseName=MotorInsurance;trustServerCertificate=true" \
  --username=sa --password=YourPassword update
```

### 2. Backend

```bash
cd backend
dotnet restore
dotnet build
dotnet test           # state machine + slice tests (xUnit)
dotnet run --project src/MotorInsurance.Api   # → http://localhost:5000, Swagger ที่ /swagger
```

ตั้ง connection string ใน `src/MotorInsurance.Api/appsettings.json` (`ConnectionStrings:Default`)

### 3. Frontend

```bash
cd frontend
npm install
npm run dev           # → http://localhost:3000
```

`NEXT_PUBLIC_API_URL` ตั้งใน `.env.local` (ค่าเริ่มต้น `http://localhost:5000/api`)

---

## Workflow ที่ครอบคลุม

| Workflow | Command/Query | Endpoint |
|----------|---------------|----------|
| แก้ไขลูกค้า | `UpdateCustomerEndpoint` | `PUT /api/customers/{id}` |
| ลบลูกค้า | `DeleteCustomerEndpoint` | `DELETE /api/customers/{id}` |
| อัปโหลดรูปบัตรผู้ขับขี่ | `UploadIdCardEndpoint` | `POST /api/uploads/id-card` (multipart) |
| สร้างใบเสนอราคา (ผู้ขับขี่ 1–5 คน + NCB/deductible/riders) | `CreateQuotationEndpoint` | `POST /api/quotations` |
| ดูตัวอย่างเบี้ยแบบ breakdown (ไม่บันทึก) | `PreviewPremiumEndpoint` | `POST /api/quotations/preview` |
| เปรียบเทียบเบี้ยทุกชั้น (+ PDF) | `CompareCoverage/DocumentEndpoint` | `POST /api/quotations/compare` · `/compare/document` |
| PDF ใบเสนอราคา / ส่งอีเมลถึงลูกค้า | `GetQuotationDocument/SendQuotationEmailEndpoint` | `GET /api/quotations/{id}/document` · `POST /{id}/email` |
| ออกกรมธรรม์ (+ แจ้งเตือนลูกค้า) | `IssuePolicyEndpoint` | `POST /api/policies/issue` |
| เปิดใช้งาน | `ActivatePolicyEndpoint` | `POST /api/policies/{id}/activate` |
| ยกเลิก (+ คืนเบี้ย pro-rata) | `CancelPolicyEndpoint` | `POST /api/policies/{id}/cancel` |
| **ต่ออายุ** (+ auto-NCB) | `RenewPolicyEndpoint` | `POST /api/renewals/{policyId}` |
| worklist กรมธรรม์ใกล้หมดอายุ | `GetExpiringPoliciesEndpoint` | `GET /api/renewals/expiring?days=60` |
| ส่งแจ้งเตือนต่ออายุ | `SendRenewalReminderEndpoint` | `POST /api/renewals/{id}/remind` |
| ประวัติการแจ้งเตือน / ส่งซ้ำ | `ListNotifications/ResendNotificationEndpoint` | `GET /api/notifications` · `POST /{id}/resend` |
| แจ้งเคลม | `FileClaimEndpoint` | `POST /api/claims` |
| เลื่อนสถานะเคลม | `AdvanceClaimEndpoint` | `POST /api/claims/{id}/advance` |
| อนุมัติ (+ แจ้งเตือนลูกค้า) / ปฏิเสธเคลม | `Approve/RejectClaimEndpoint` | `POST /api/claims/{id}/approve` · `/reject` |
| ดูรายละเอียดเคลม | `GetClaimEndpoint` | `GET /api/claims/{id}` |
| worklist เคลมตาม SLA (อายุในสถานะ) | `ClaimsAgingEndpoint` | `GET /api/claims/aging` |
| จดหมายแจ้งผลสินไหม (PDF) | `GetClaimLetterEndpoint` | `GET /api/claims/{id}/letter` |
| มอบหมายอู่/ผู้สำรวจภัย | `AssignClaimEndpoint` | `POST /api/claims/{id}/assign` |
| แนบรูปความเสียหาย | `UploadClaimPhotoEndpoint` | `POST /api/claims/{id}/photos` (multipart) |
| ชำระเงิน | `SettlePaymentEndpoint` | `POST /api/payments/{id}/settle` |
| QR พร้อมเพย์ (เบี้ยรอชำระ) | `GetPromptPayQrEndpoint` | `GET /api/payments/{id}/promptpay-qr` (PNG) |
| สลักหลังกรมธรรม์ (แก้ข้อมูลลูกค้า) | `CreateEndorsementEndpoint` | `POST /api/policies/{policyId}/endorsements` |
| สลักหลังปรับความคุ้มครอง (+ เบี้ย pro-rata) | `CoverageEndorsementEndpoint` | `POST /api/policies/{id}/coverage-endorsement` |
| ดูกรมธรรม์ / ประวัติ (temporal) | `GetPolicies/PolicyHistoryEndpoint` | `GET /api/policies` · `/{id}/history` |
| ไทม์ไลน์กิจกรรมกรมธรรม์ (audit) | `GetPolicyActivityEndpoint` | `GET /api/policies/{id}/activity` |
| PDF ตารางกรมธรรม์ / ใบเสร็จ | `GetPolicyDocument/PaymentReceiptEndpoint` | `GET /api/policies/{id}/document` · `/api/payments/{id}/receipt` |
| แดชบอร์ดงานค้าง (counts) / รายงาน-วิเคราะห์ | `DashboardSummary/AnalyticsEndpoint` | `GET /api/dashboard/summary` · `/api/reports/analytics` |
| จัดการผู้ใช้/บทบาท (ADMIN) | `User/RoleEndpoints` | `GET/POST/PUT/DELETE /api/users` · `GET /api/roles` · `POST /api/users/{id}/reset-password` |

### Master data (CRUD — ใช้ permission `lookup.read` / `lookup.manage`)

| ชุดข้อมูล | Endpoint |
|----------|----------|
| ข้อมูลหลักรถยนต์ (ยี่ห้อ → รุ่น → รุ่นย่อย → ปี) | `GET/POST/PUT/DELETE /api/lookups/vehicle-brands` ฯลฯ |
| ที่อยู่ (จังหวัด/อำเภอ/ตำบล/รหัสไปรษณีย์) | `GET /api/lookups/provinces` ฯลฯ |
| คำนำหน้าชื่อลูกค้า | `GET/POST/PUT/DELETE /api/lookups/customer-titles` |
| ความคุ้มครองเสริม (rider) — มีเบี้ยต่อรายการ | `GET/POST/PUT/DELETE /api/lookups/riders` |
| อู่/ศูนย์ซ่อม (garage) — สำหรับมอบหมายเคลม | `GET/POST/PUT/DELETE /api/lookups/garages` |
| พิกัดอัตราเบี้ย (effective-dated) — perm `rating.read`/`rating.manage` | `GET/POST/PUT /api/lookups/premium-rates` |
| โหลดตามอายุรถ (age band) — perm `rating.*` | `GET/POST/PUT/DELETE /api/lookups/age-loading-bands` |
| ตั้งค่าคิดเบี้ย (ส่วนลด deductible) — perm `rating.*` | `GET/PUT /api/lookups/rating-settings` |

หน้า **ข้อมูลหลัก** (`/master`) จัดการชุดข้อมูลเหล่านี้ทั้งหมด (รวม **พิกัดอัตราเบี้ย** ที่ `/master/rates`),
หน้า **ผู้ใช้งานระบบ** (`/admin/users`, ADMIN) และหน้า **การแจ้งเตือน** (`/notifications`)

### Flow ต่ออายุ (renewal)

1. ตรวจกรมธรรม์เดิมต้อง `Active` และอยู่ในช่วง 60 วันก่อนหมดอายุ
2. กันต่ออายุซ้ำ (เช็ค `previous_policy_id`)
3. สร้างกรมธรรม์ใหม่สถานะ `Issued` ผูกกับเดิมผ่าน `PreviousPolicyId`
4. effective = วันถัดจากกรมธรรม์เดิมหมดอายุ
5. **ปรับ NCB อัตโนมัติ**: ปีก่อนไม่มีเคลม → เลื่อนขั้นส่วนลดขึ้น (0→20→30→40→50%), มีเคลม → รีเซ็ตเป็น 0 แล้วคิดเบี้ยใหม่ (carry deductible + riders จากกรมธรรม์เดิม)
6. สร้าง payment เบี้ย (inbound, pending) — จ่ายแล้วจึงเปิดใช้งาน

### การคิดเบี้ยขั้นสูง (premium rating)

`PremiumCalculator` (pure, ทดสอบได้) คืน **breakdown** ทีละชั้น — ฟอร์มใบเสนอราคาเรียก `POST /api/quotations/preview` เพื่อโชว์เบี้ยสด:

```
เบี้ยฐาน        = ทุนประกัน × อัตราตามชั้น (Type1 .045 / Type2+ .030 / Type3+ .022 / Type3 .015)
+ โหลดอายุรถ    = เบี้ยฐาน × (≤5ปี 0% / 6–10ปี 5% / >10ปี 10%)
− ส่วนลด NCB    = (ฐาน+โหลด) × NCB% (0/20/30/40/50)
− ส่วนลด deductible = min(ค่าเสียหายส่วนแรก × 0.5, ฐาน × 20%)
+ ความคุ้มครองเสริม = ผลรวมเบี้ย rider ที่เลือก
= เบี้ยสุทธิ (ไม่ต่ำกว่า 0)
```

> อัตรา/factor เริ่มต้นเป็น **ค่าสมมติ** ไม่ใช่พิกัดจริง — แต่ตอนนี้ **ปรับได้ผ่าน DB** (ดู "พิกัดอัตราเบี้ยที่ปรับได้" ด้านล่าง)
> เบี้ยฐาน/NCB/deductible/riders ถูกบันทึกบน quotation แล้ว carry ต่อไปยัง policy ตอนออกกรมธรรม์

#### พิกัดอัตราเบี้ยที่ปรับได้ (configurable + effective-dated)

`PremiumRatingService` resolve **factor จากตาราง** แล้วส่งเข้า `PremiumCalculator.Rate(input, RateFactors)` (ถ้าตารางว่างจะ fallback เป็นค่า built-in เดิม — unit test จึงยังได้ตัวเลขเดิม):
- `premium_rate` — อัตราเบี้ยฐานต่อชั้น **มีเวอร์ชันตามวันที่มีผล** (`effective_date`) → เลือกแถวล่าสุดที่ `effective_date ≤` ปีที่อ้างอิง (ใบเสนอราคาเก่าคิดเบี้ยซ้ำได้ตรงเดิม)
- `age_loading_band` — โหลดตามอายุรถ (ช่วง `max_age` แบบ inclusive, `NULL` = ช่วงปลายเปิด) แบบ effective-dated
- `rating_setting` — ค่า scalar เช่น `DEDUCTIBLE_RELIEF_RATE` / `DEDUCTIBLE_RELIEF_CAP`

จัดการที่หน้า `/master/rates` (perm `rating.manage`)

### ออกเอกสาร PDF (QuestPDF)

- **ตารางกรมธรรม์** (`/api/policies/{id}/document`, A4) — ข้อมูลผู้เอาประกัน/รถ/ความคุ้มครอง + **breakdown เบี้ยทีละชั้น**
- **ใบเสร็จรับเงิน** (`/api/payments/{id}/receipt`, A5) — เฉพาะรายการรับเบี้ยที่ชำระแล้ว
- **ใบเสนอราคา** (`/api/quotations/{id}/document`, A4) — re-rate breakdown สด + **ส่งอีเมลแนบ PDF** ถึงลูกค้า (`POST /{id}/email`)
- **ใบเปรียบเทียบความคุ้มครอง** (`/api/quotations/compare/document`, A4 แนวนอน) — เบี้ยทั้ง 4 ชั้นเทียบกัน
- **จดหมายแจ้งผลสินไหม** (`/api/claims/{id}/letter`, A4) — เฉพาะเคลมที่พิจารณาแล้ว (Approved/Rejected/Paid/Closed)
- ฝังฟอนต์ **Sarabun (OFL)** ใน Api เป็น EmbeddedResource + format วันที่เป็น พ.ศ. (th-TH) ให้ตรงกับ UI
- FE ดึงเป็น object URL (serializable) ผ่าน auth/refresh แล้วดาวน์โหลด

### แจ้งเตือนหลายช่องทาง + งานเบื้องหลัง

- **ช่องทางจริง**: `INotificationSender` เลือก impl จาก config `Notifications:Channel` — `Log` (default), `Smtp` (System.Net.Mail, แนบ PDF ได้), `Line` (LINE Notify ผ่าน `IHttpClientFactory`) โดยไม่แตะ call site
- **เลือกผู้รับ**: email → phone → log; ทุกครั้งบันทึกลงตาราง `notification` (Sent/Failed)
- **ประวัติ + ส่งซ้ำ**: หน้า `/notifications` (`GET /api/notifications`) + ปุ่มส่งซ้ำรายการที่ Failed (`POST /api/notifications/{id}/resend`)
- **แจ้งตาม event**: ออกกรมธรรม์ / อนุมัติเคลม → แจ้งลูกค้าอัตโนมัติ (best-effort ผ่าน `NotificationDispatcher`, ไม่ทำให้ flow หลักล้ม)
- **งานเบื้องหลัง** (`PolicyLifecycleWorker`, `BackgroundService`): รันตามรอบ (config `PolicyLifecycle`) เพื่อ (1) flip กรมธรรม์ `Active` ที่เลยวันหมดอายุ → `Expired` ผ่าน state machine และ (2) ส่งเตือนต่ออายุอัตโนมัติในหน้าต่างก่อนหมดอายุ (กันส่งซ้ำด้วย throttle)

### ต่ออายุเชิงรุก

- **worklist** กรมธรรม์ `Active` ที่ใกล้หมดอายุภายใน N วันและยังไม่ต่ออายุ (หน้า `/renewals`) — เตือนเองหรือให้ worker เตือนอัตโนมัติ

### คืนเบี้ย pro-rata เมื่อยกเลิก

ยกเลิกกรมธรรม์ที่ `Active` (จ่ายเบี้ยแล้ว) → คืนเบี้ยตามวันที่เหลือ `premium × วันคงเหลือ / วันทั้งหมด` เป็น **outbound payment รอชำระ** ให้ฝ่ายการเงิน settle (Issued ที่ยังไม่จ่าย → คืน 0)

### ชำระออนไลน์ด้วย PromptPay QR

`GET /api/payments/{id}/promptpay-qr` สร้าง **EMVCo PromptPay payload + CRC-16** ระบุจำนวนเงิน แล้ว render เป็น PNG (QRCoder) — payee ตั้งใน `PromptPay:Target` (appsettings)

### รายงาน/วิเคราะห์ (หน้า `/reports`)

`GET /api/reports/analytics` คืน: เบี้ยรับรายเดือน 12 เดือน (zero-filled), loss ratio (สินไหมจ่าย/เบี้ยรับ), และจำนวนตามสถานะกรมธรรม์/ชั้น/สถานะเคลม — FE วาดกราฟเองไม่พึ่ง chart lib

### เคลม: อู่/ผู้สำรวจภัย/รูปความเสียหาย

มอบหมายอู่ (master) + ผู้สำรวจภัยต่อเคลม และแนบรูปความเสียหาย (ผ่าน `IFileStorage` เดิม) — จัดการผ่าน dialog "จัดการ" ในหน้าเคลม

### เคลม: worklist ตาม SLA (aging)

`GET /api/claims/aging` อ่าน **เวลาที่เคลมเข้าสถานะปัจจุบัน** จากตาราง temporal `claim_history` (`IClaimAgingReader` ใช้ `TemporalAll()`) แล้วคำนวณจำนวนวันในสถานะเทียบ SLA ต่อสถานะ — แสดงรายการที่ **เกินกำหนดก่อน** บน panel ในหน้า `/claims`

### สลักหลังปรับความคุ้มครอง + เบี้ยส่วนต่าง

`POST /api/policies/{id}/coverage-endorsement` (กรมธรรม์ `Active`): เปลี่ยนชั้น/ทุนประกัน/riders → **re-rate** ด้วย NCB/deductible เดิม → คิดส่วนต่างเบี้ยแบบ **pro-rata ตามวันคงเหลือ** → ออก payment **รับเพิ่ม (inbound)** หรือ **คืน (outbound)** รอชำระ พร้อมบันทึก endorsement ต่อ field และอัปเดตเบี้ย/ความคุ้มครองบนกรมธรรม์

### ไทม์ไลน์กิจกรรม (audit) + แดชบอร์ดงานค้าง

- `GET /api/policies/{id}/activity` รวมเหตุการณ์จาก temporal history (เปลี่ยนสถานะ) + endorsements + payments + notifications เรียงใหม่→เก่า แสดงในหน้า policy detail
- แดชบอร์ด (`/`) เพิ่มการ์ด **"งานค้างที่ต้องติดตาม"**: เคลมเกิน SLA / กรมธรรม์ใกล้หมดอายุ / แจ้งเตือนที่ส่งล้มเหลว (ดึงจาก endpoint ที่มีอยู่ + gate ตาม permission)

### จัดการผู้ใช้/บทบาท (ADMIN)

หน้า `/admin/users`: CRUD ผู้ใช้ + assign บทบาท + ตั้งรหัสผ่านใหม่ (ใช้ `IPasswordHasher` ตัวเดียวกับ login, revoke refresh token เดิม) — guard ห้ามเหลือ ADMIN ที่ใช้งานอยู่ 0 คน และห้ามลบบัญชีตนเอง (perm `user.read` / `user.manage`)

---

## End-to-end ตัวอย่าง (curl)

> ตัวอย่างละ token ออกเพื่อความกระชับ — จริง ๆ ต้องแนบ `-H 'Authorization: Bearer <token>'`

```bash
# 0) อัปโหลดรูปบัตรผู้ขับขี่ (คืน path ที่ใช้แนบใน quotation)
curl -X POST localhost:5000/api/uploads/id-card -F 'file=@idcard.jpg'
#   → { "path": "uploads/idcards/xxxx.jpg" }

# 1) ดูตัวอย่างเบี้ยแบบ breakdown ก่อน (ไม่บันทึก)
curl -X POST localhost:5000/api/quotations/preview -H 'Content-Type: application/json' \
  -d '{"vehicleId":1,"coverageType":"Type1","sumInsured":1000000,"ncbPercent":30,"deductible":5000,"riderIds":[1,4]}'
#   → { "basePremium":45000, "ncbDiscount":13500, "deductibleDiscount":2500, "ridersTotal":2700, "netPremium":31700, ... }

# 2) สร้าง quotation พร้อมผู้ขับขี่ระบุชื่อ (1–5 คน + รูปบัตร) และพารามิเตอร์คิดเบี้ย (NCB/deductible/riders ไม่บังคับ)
curl -X POST localhost:5000/api/quotations -H 'Content-Type: application/json' \
  -d '{"customerId":1,"vehicleId":1,"coverageType":"Type1","sumInsured":500000,
       "ncbPercent":20,"deductible":2000,"riderIds":[2],
       "drivers":[{"fullName":"สมชาย ใจดี","nationalId":"1100000000001","idCardImagePath":"uploads/idcards/xxxx.jpg"}]}'

# 3) ออกกรมธรรม์จาก quotation
curl -X POST localhost:5000/api/policies/issue -H 'Content-Type: application/json' \
  -d '{"quotationId":1,"effectiveDate":"2026-06-01"}'

# 4) จ่ายเบี้ย (settle premium payment) → policy เปิดใช้งานอัตโนมัติ
curl -X POST localhost:5000/api/payments/1/settle -H 'Content-Type: application/json' \
  -d '{"referenceNo":"TXN-001"}'

# 5) ต่ออายุ (NCB ปรับอัตโนมัติตามประวัติเคลม)
curl -X POST localhost:5000/api/renewals/1 -H 'Content-Type: application/json' -d '{}'

# 6) แก้ข้อมูลลูกค้าที่มีกรมธรรม์แล้ว → ต้องสลักหลัง (แก้ตรง ๆ ผ่าน PUT /api/customers/{id} จะได้ 409)
curl -X POST localhost:5000/api/policies/1/endorsements -H 'Content-Type: application/json' \
  -d '{"phone":"0899999999","effectiveDate":"2026-06-10","note":"ลูกค้าแจ้งเปลี่ยนเบอร์"}'

# 7) ดาวน์โหลด PDF ตารางกรมธรรม์ / QR พร้อมเพย์ของเบี้ยรอชำระ
curl -L localhost:5000/api/policies/1/document -o policy.pdf
curl -L localhost:5000/api/payments/1/promptpay-qr -o promptpay.png

# 8) ยกเลิกกรมธรรม์ (Active) → ได้เบี้ยคืน pro-rata เป็น outbound payment รอชำระ
curl -X POST localhost:5000/api/policies/1/cancel -H 'Content-Type: application/json' \
  -d '{"reason":"ลูกค้าขอยกเลิก"}'
#   → { "refundAmount": 2642.86, "refundPaymentNo": "PAY-2026-000010" }

# 9) worklist ต่ออายุ + ส่งแจ้งเตือน, และรายงานวิเคราะห์
curl localhost:5000/api/renewals/expiring?days=60
curl -X POST localhost:5000/api/renewals/1/remind
curl localhost:5000/api/reports/analytics

# 10) เปรียบเทียบเบี้ยทุกชั้น + PDF ใบเสนอราคา + ส่งอีเมลถึงลูกค้า
curl -X POST localhost:5000/api/quotations/compare -H 'Content-Type: application/json' \
  -d '{"vehicleId":1,"sumInsured":1000000,"ncbPercent":30}'
curl -L localhost:5000/api/quotations/1/document -o quotation.pdf
curl -X POST localhost:5000/api/quotations/1/email

# 11) สลักหลังปรับความคุ้มครอง (Active) → ได้ส่วนต่างเบี้ย pro-rata เป็น payment รอชำระ
curl -X POST localhost:5000/api/policies/1/coverage-endorsement -H 'Content-Type: application/json' \
  -d '{"newSumInsured":1200000,"effectiveDate":"2026-06-10"}'
#   → { "newPremium": 54000, "premiumDelta": 14250.0, "paymentNo": "PAY-2026-000011" }

# 12) worklist เคลม SLA, จดหมายแจ้งผลสินไหม, ไทม์ไลน์กิจกรรม, ประวัติแจ้งเตือน
curl localhost:5000/api/claims/aging
curl -L localhost:5000/api/claims/1/letter -o claim-letter.pdf
curl localhost:5000/api/policies/1/activity
curl localhost:5000/api/notifications

# 13) ปรับพิกัดอัตราเบี้ย (perm rating.manage) — เพิ่มเวอร์ชันอัตราใหม่ที่มีผลตามวันที่
curl -X POST localhost:5000/api/lookups/premium-rates -H 'Content-Type: application/json' \
  -d '{"coverage":"Type1","rate":0.05,"effectiveDate":"2027-01-01"}'
```

---

## หมายเหตุสำคัญ

- **Backend `dotnet build` + `dotnet test` (68 tests) ผ่าน** และ **Frontend ผ่าน `tsc --noEmit` + `next build`** (ทุก route compile)
  - ⚠️ `npm run lint` ใช้ไม่ได้บน Next.js 16 (ถอด `next lint` ออกแล้ว) — ใช้ `npm run typecheck` + `npm run build` แทน
- **Liquibase เป็นเจ้าของ schema** (changelog `V001`..`V021`) — ห้ามรัน `dotnet ef migrations add` กับโปรเจกต์นี้ จะไป generate temporal/comment ทับ Liquibase แล้ว drift
- ทุกครั้งที่แก้ schema ใน Liquibase ต้องอัปเดต entity + Configuration เอง แล้วให้ test (รันบน SQL Server จริง เช่น Testcontainers) เป็นด่านจับ drift
- EF InMemory test ครอบ logic เท่านั้น — temporal table / rowversion ต้องทดสอบบน SQL Server จริง (test helper กลาง: `InMemoryAppDb` ใน `tests`)
- Document number generator ใช้ `UPDATE ... WITH (UPDLOCK, SERIALIZABLE)` — race-free ข้าม instance และ reset ทุกปี
- **NuGet ที่เพิ่ม**: QuestPDF (PDF, Community license), QRCoder (PromptPay QR) — ฟอนต์ Sarabun (OFL) ฝังใน `Api/Assets/fonts`; **แจ้งเตือน SMTP/LINE ไม่เพิ่ม NuGet** (ใช้ `System.Net.Mail` + `IHttpClientFactory`)
- **การส่งแจ้งเตือนจริง**: ต่อ SMTP/LINE ได้แล้วผ่าน config `Notifications:Channel` (`Log`/`Smtp`/`Line`) โดยไม่แตะ endpoint — default ยังเป็น `Log`
- **อัตราคิดเบี้ยเริ่มต้นเป็นค่าสมมติ** แต่ปรับได้ผ่านตาราง `premium_rate` / `age_loading_band` / `rating_setting` (effective-dated, fallback เป็นค่า built-in เมื่อตารางว่าง)
