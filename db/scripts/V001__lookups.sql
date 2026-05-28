-- Lookup / state-machine seed tables (SQL Server). Liquibase owns schema.

CREATE TABLE policy_status (
    code VARCHAR(20) NOT NULL CONSTRAINT pk_policy_status PRIMARY KEY,
    name_th NVARCHAR(100) NOT NULL, name_en VARCHAR(100) NOT NULL,
    is_terminal BIT NOT NULL CONSTRAINT df_ps_term DEFAULT (0),
    sort_order INT NOT NULL CONSTRAINT df_ps_sort DEFAULT (0)
);
INSERT INTO policy_status (code, name_th, name_en, is_terminal, sort_order) VALUES
 ('Draft',N'ฉบับร่าง','Draft',0,1),('Quoted',N'เสนอราคาแล้ว','Quoted',0,2),
 ('Issued',N'ออกกรมธรรม์แล้ว','Issued',0,3),('Active',N'คุ้มครองอยู่','Active',0,4),
 ('Cancelled',N'ยกเลิก','Cancelled',1,5),('Expired',N'หมดอายุ','Expired',1,6);

CREATE TABLE claim_status (
    code VARCHAR(20) NOT NULL CONSTRAINT pk_claim_status PRIMARY KEY,
    name_th NVARCHAR(100) NOT NULL, name_en VARCHAR(100) NOT NULL,
    is_terminal BIT NOT NULL CONSTRAINT df_cs_term DEFAULT (0),
    sort_order INT NOT NULL CONSTRAINT df_cs_sort DEFAULT (0)
);
INSERT INTO claim_status (code, name_th, name_en, is_terminal, sort_order) VALUES
 ('Filed',N'แจ้งเคลม','Filed',0,1),('UnderReview',N'กำลังตรวจสอบ','Under review',0,2),
 ('Assessment',N'ประเมินความเสียหาย','Assessment',0,3),('Approved',N'อนุมัติ','Approved',0,4),
 ('Rejected',N'ปฏิเสธ','Rejected',1,5),('Paid',N'จ่ายค่าสินไหมแล้ว','Paid',0,6),
 ('Closed',N'ปิดเคลม','Closed',1,7);

CREATE TABLE payment_status (
    code VARCHAR(20) NOT NULL CONSTRAINT pk_payment_status PRIMARY KEY,
    name_th NVARCHAR(100) NOT NULL, name_en VARCHAR(100) NOT NULL
);
INSERT INTO payment_status (code, name_th, name_en) VALUES
 ('Pending',N'รอชำระ','Pending'),('Paid',N'ชำระแล้ว','Paid'),
 ('Failed',N'ชำระไม่สำเร็จ','Failed'),('Refunded',N'คืนเงินแล้ว','Refunded');
