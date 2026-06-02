# ระบบประกันรถยนต์ — Motor Insurance Workflow

Full-stack ครอบคลุม workflow ประกันรถยนต์: quotation → policy → claim → payment → **renewal**

- **Backend**: .NET 8, Clean Architecture, CQRS (MediatR), Entity Framework Core, SQL Server
- **Frontend**: Next.js 14 (App Router), RTK Query, Tailwind CSS
- **Database**: SQL Server — schema เป็นเจ้าของโดย **Liquibase** (EF = ORM อย่างเดียว ไม่ migrate)

---

## โครงสร้าง

```
backend/
  MotorInsurance.sln
  src/
    MotorInsurance.Domain/          # entities, enums, state machines (ไม่พึ่ง layer อื่น)
    MotorInsurance.Application/     # CQRS handlers, validators, behaviors, IAppDbContext
    MotorInsurance.Infrastructure/  # AppDbContext, EF configs, services, DI
    MotorInsurance.Api/             # controllers, exception middleware, Program.cs
  tests/MotorInsurance.Tests/       # state machine + handler slice tests
db/
  db.changelog-master.xml           # Liquibase (SQL-first + rollback)
  scripts/V001__lookups.sql, V002__core.sql
frontend/
  src/app/                          # App Router pages (policies list + detail)
  src/lib/api/insuranceApi.ts       # RTK Query (ทุก endpoint)
  src/lib/store/                    # Redux store + provider
  src/components/                   # StatusBadge
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
dotnet build          # ⚠️ ตรวจ compile ที่นี่ (ดู "หมายเหตุสำคัญ" ด้านล่าง)
dotnet test           # state machine + slice tests
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
| สร้างใบเสนอราคา (พร้อมผู้ขับขี่ 1–5 คน) | `CreateQuotationEndpoint` | `POST /api/quotations` |
| ออกกรมธรรม์ | `IssuePolicyCommand` | `POST /api/policies/issue` |
| เปิดใช้งาน | `ActivatePolicyCommand` | `POST /api/policies/{id}/activate` |
| ยกเลิก | `CancelPolicyCommand` | `POST /api/policies/{id}/cancel` |
| **ต่ออายุ** | `RenewPolicyCommand` | `POST /api/renewals/{policyId}` |
| แจ้งเคลม | `FileClaimCommand` | `POST /api/claims` |
| เลื่อนสถานะเคลม | `AdvanceClaimCommand` | `POST /api/claims/{id}/advance` |
| อนุมัติเคลม | `ApproveClaimCommand` | `POST /api/claims/{id}/approve` |
| ปฏิเสธเคลม | `RejectClaimCommand` | `POST /api/claims/{id}/reject` |
| ชำระเงิน | `SettlePaymentCommand` | `POST /api/payments/{id}/settle` |
| สลักหลังกรมธรรม์ (แก้ข้อมูลลูกค้า) | `CreateEndorsementEndpoint` | `POST /api/policies/{policyId}/endorsements` |
| ดูกรมธรรม์ | `GetPoliciesQuery` / `GetPolicyByIdQuery` | `GET /api/policies` |
| ดูประวัติ (temporal) | `GetPolicyHistoryQuery` | `GET /api/policies/{id}/history` |

### Flow ต่ออายุ (renewal) — ที่ขอเพิ่ม

1. ตรวจกรมธรรม์เดิมต้อง `Active` และอยู่ในช่วง 60 วันก่อนหมดอายุ
2. กันต่ออายุซ้ำ (เช็ค `previous_policy_id`)
3. สร้างกรมธรรม์ใหม่สถานะ `Issued` ผูกกับเดิมผ่าน `PreviousPolicyId`
4. effective = วันถัดจากกรมธรรม์เดิมหมดอายุ, คำนวณเบี้ยใหม่
5. สร้าง payment เบี้ย (inbound, pending) — จ่ายแล้วจึงเปิดใช้งาน

---

## End-to-end ตัวอย่าง (curl)

> ตัวอย่างละ token ออกเพื่อความกระชับ — จริง ๆ ต้องแนบ `-H 'Authorization: Bearer <token>'`

```bash
# 0) อัปโหลดรูปบัตรผู้ขับขี่ (คืน path ที่ใช้แนบใน quotation)
curl -X POST localhost:5000/api/uploads/id-card -F 'file=@idcard.jpg'
#   → { "path": "uploads/idcards/xxxx.jpg" }

# 1) สร้าง quotation พร้อมผู้ขับขี่ระบุชื่อ (1–5 คน + รูปบัตร)
curl -X POST localhost:5000/api/quotations -H 'Content-Type: application/json' \
  -d '{"customerId":1,"vehicleId":1,"coverageType":"Type1","sumInsured":500000,
       "drivers":[{"fullName":"สมชาย ใจดี","nationalId":"1100000000001","idCardImagePath":"uploads/idcards/xxxx.jpg"}]}'

# 2) ออกกรมธรรม์จาก quotation
curl -X POST localhost:5000/api/policies/issue -H 'Content-Type: application/json' \
  -d '{"quotationId":1,"effectiveDate":"2026-06-01"}'

# 3) จ่ายเบี้ย (settle premium payment) → policy เปิดใช้งานอัตโนมัติ
curl -X POST localhost:5000/api/payments/1/settle -H 'Content-Type: application/json' \
  -d '{"referenceNo":"TXN-001"}'

# 4) ต่ออายุ
curl -X POST localhost:5000/api/renewals/1 -H 'Content-Type: application/json' -d '{}'

# 5) แก้ข้อมูลลูกค้าที่มีกรมธรรม์แล้ว → ต้องสลักหลัง (แก้ตรง ๆ ผ่าน PUT /api/customers/{id} จะได้ 409)
curl -X POST localhost:5000/api/policies/1/endorsements -H 'Content-Type: application/json' \
  -d '{"phone":"0899999999","effectiveDate":"2026-06-10","note":"ลูกค้าแจ้งเปลี่ยนเบอร์"}'
```

---

## หมายเหตุสำคัญ

- **Backend ยังไม่ถูก compile ในเครื่องที่สร้าง** (ไม่มี .NET SDK + network จำกัด) ต้อง `dotnet build` เองหลังโหลด อาจเจอ error เล็กน้อย — โครงและ signature ตรวจด้วยมือแล้ว
- **Frontend ผ่าน `tsc --noEmit` และ `next build` จริง** ทุก route compile สำเร็จ
- **Liquibase เป็นเจ้าของ schema** — ห้ามรัน `dotnet ef migrations add` กับโปรเจกต์นี้ จะไป generate temporal/comment ทับ Liquibase แล้ว drift
- ทุกครั้งที่แก้ schema ใน Liquibase ต้องอัปเดต entity + Configuration เอง แล้วให้ test (รันบน SQL Server จริง เช่น Testcontainers) เป็นด่านจับ drift
- EF InMemory test ครอบ logic เท่านั้น — temporal table / rowversion ต้องทดสอบบน SQL Server จริง
- Document number generator ใช้ `COUNT + lock` แบบง่าย — production ควรเปลี่ยนเป็น DB sequence ต่อ prefix
