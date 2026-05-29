-- Authentication & authorization (SQL Server). RBAC: app_user -> role -> permission.
-- Liquibase owns schema. Permissions/roles/role_permission are reference data and are
-- seeded here; users + user_role are seeded by the app (AuthDataSeeder) so passwords are
-- hashed by the same PBKDF2 routine the API verifies against.

CREATE TABLE app_user (
    id BIGINT IDENTITY(1,1) CONSTRAINT pk_app_user PRIMARY KEY,
    username VARCHAR(50) NOT NULL,
    email VARCHAR(255) NOT NULL,
    password_hash VARCHAR(400) NOT NULL,
    full_name NVARCHAR(200) NOT NULL,
    is_active BIT NOT NULL CONSTRAINT df_app_user_active DEFAULT (1),
    created_at DATETIME2 NOT NULL CONSTRAINT df_app_user_created DEFAULT (SYSUTCDATETIME()),
    last_login_at DATETIME2 NULL
);
CREATE UNIQUE INDEX ux_app_user_username ON app_user (username);
CREATE UNIQUE INDEX ux_app_user_email ON app_user (email);

CREATE TABLE role (
    id BIGINT IDENTITY(1,1) CONSTRAINT pk_role PRIMARY KEY,
    code VARCHAR(30) NOT NULL,
    name_th NVARCHAR(100) NOT NULL,
    name_en VARCHAR(100) NOT NULL
);
CREATE UNIQUE INDEX ux_role_code ON role (code);

CREATE TABLE permission (
    code VARCHAR(60) NOT NULL CONSTRAINT pk_permission PRIMARY KEY,
    name_th NVARCHAR(150) NOT NULL,
    name_en VARCHAR(150) NOT NULL,
    category VARCHAR(40) NOT NULL
);

CREATE TABLE role_permission (
    role_id BIGINT NOT NULL,
    permission_code VARCHAR(60) NOT NULL,
    CONSTRAINT pk_role_permission PRIMARY KEY (role_id, permission_code),
    CONSTRAINT fk_role_permission_role FOREIGN KEY (role_id) REFERENCES role (id) ON DELETE CASCADE,
    CONSTRAINT fk_role_permission_perm FOREIGN KEY (permission_code) REFERENCES permission (code) ON DELETE CASCADE
);

CREATE TABLE user_role (
    user_id BIGINT NOT NULL,
    role_id BIGINT NOT NULL,
    CONSTRAINT pk_user_role PRIMARY KEY (user_id, role_id),
    CONSTRAINT fk_user_role_user FOREIGN KEY (user_id) REFERENCES app_user (id) ON DELETE CASCADE,
    CONSTRAINT fk_user_role_role FOREIGN KEY (role_id) REFERENCES role (id) ON DELETE CASCADE
);

CREATE TABLE refresh_token (
    id BIGINT IDENTITY(1,1) CONSTRAINT pk_refresh_token PRIMARY KEY,
    user_id BIGINT NOT NULL,
    token_hash CHAR(64) NOT NULL,                -- SHA-256 hex of the raw token
    expires_at DATETIME2 NOT NULL,
    created_at DATETIME2 NOT NULL CONSTRAINT df_refresh_token_created DEFAULT (SYSUTCDATETIME()),
    revoked_at DATETIME2 NULL,
    replaced_by_hash CHAR(64) NULL,
    CONSTRAINT fk_refresh_token_user FOREIGN KEY (user_id) REFERENCES app_user (id) ON DELETE CASCADE
);
CREATE UNIQUE INDEX ux_refresh_token_hash ON refresh_token (token_hash);
CREATE INDEX ix_refresh_token_user ON refresh_token (user_id);

-- ----- Reference data: permissions -----
INSERT INTO permission (code, name_th, name_en, category) VALUES
 ('customer.read',   N'ดูข้อมูลลูกค้า',        'View customers',     'Customer'),
 ('customer.write',  N'จัดการลูกค้า',           'Manage customers',   'Customer'),
 ('vehicle.read',    N'ดูข้อมูลรถยนต์',         'View vehicles',      'Vehicle'),
 ('vehicle.write',   N'จัดการรถยนต์',           'Manage vehicles',    'Vehicle'),
 ('lookup.read',     N'ดูข้อมูลหลักรถยนต์',     'View vehicle master','Lookup'),
 ('lookup.manage',   N'จัดการข้อมูลหลักรถยนต์', 'Manage vehicle master','Lookup'),
 ('quotation.read',  N'ดูใบเสนอราคา',           'View quotations',    'Quotation'),
 ('quotation.write', N'สร้างใบเสนอราคา',        'Create quotations',  'Quotation'),
 ('policy.read',     N'ดูกรมธรรม์',             'View policies',      'Policy'),
 ('policy.issue',    N'ออกกรมธรรม์',            'Issue policy',       'Policy'),
 ('policy.activate', N'เปิดใช้กรมธรรม์',         'Activate policy',    'Policy'),
 ('policy.cancel',   N'ยกเลิกกรมธรรม์',          'Cancel policy',      'Policy'),
 ('policy.renew',    N'ต่ออายุกรมธรรม์',         'Renew policy',       'Policy'),
 ('claim.read',      N'ดูเคลม',                 'View claims',        'Claim'),
 ('claim.file',      N'แจ้งเคลม',               'File claim',         'Claim'),
 ('claim.review',    N'ตรวจสอบเคลม',            'Review claim',       'Claim'),
 ('claim.approve',   N'อนุมัติเคลม',            'Approve claim',      'Claim'),
 ('claim.reject',    N'ปฏิเสธเคลม',             'Reject claim',       'Claim'),
 ('payment.read',    N'ดูการชำระเงิน',          'View payments',      'Payment'),
 ('payment.settle',  N'บันทึกการชำระเงิน',      'Settle payment',     'Payment'),
 ('dashboard.read',  N'ดูแดชบอร์ด',             'View dashboard',     'Dashboard');

-- ----- Reference data: roles -----
INSERT INTO role (code, name_th, name_en) VALUES
 ('ADMIN',       N'ผู้ดูแลระบบ',        'Administrator'),
 ('UNDERWRITER', N'เจ้าหน้าที่รับประกัน','Underwriter'),
 ('CLAIMS',      N'เจ้าหน้าที่สินไหม',   'Claims officer'),
 ('FINANCE',     N'เจ้าหน้าที่การเงิน',  'Finance officer'),
 ('VIEWER',      N'ผู้ดูข้อมูล',         'Viewer');

-- ADMIN: every permission
INSERT INTO role_permission (role_id, permission_code)
SELECT r.id, p.code FROM role r CROSS JOIN permission p WHERE r.code = 'ADMIN';

-- UNDERWRITER: customer/vehicle/quotation/policy lifecycle + lookup.read + dashboard
INSERT INTO role_permission (role_id, permission_code)
SELECT r.id, p.code FROM role r JOIN permission p
  ON p.code IN ('customer.read','customer.write','vehicle.read','vehicle.write',
                'lookup.read','quotation.read','quotation.write',
                'policy.read','policy.issue','policy.activate','policy.cancel','policy.renew',
                'dashboard.read')
WHERE r.code = 'UNDERWRITER';

-- CLAIMS: claim lifecycle + policy/payment read + dashboard
INSERT INTO role_permission (role_id, permission_code)
SELECT r.id, p.code FROM role r JOIN permission p
  ON p.code IN ('policy.read','claim.read','claim.file','claim.review','claim.approve','claim.reject',
                'payment.read','dashboard.read')
WHERE r.code = 'CLAIMS';

-- FINANCE: payment settle + policy/claim read + dashboard
INSERT INTO role_permission (role_id, permission_code)
SELECT r.id, p.code FROM role r JOIN permission p
  ON p.code IN ('payment.read','payment.settle','policy.read','claim.read','dashboard.read')
WHERE r.code = 'FINANCE';

-- VIEWER: read-only everywhere + dashboard
INSERT INTO role_permission (role_id, permission_code)
SELECT r.id, p.code FROM role r JOIN permission p
  ON p.code IN ('customer.read','vehicle.read','lookup.read','quotation.read',
                'policy.read','claim.read','payment.read','dashboard.read')
WHERE r.code = 'VIEWER';
