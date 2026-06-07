-- New permissions: user.read / user.manage (จัดการผู้ใช้งานระบบ). Granted to ADMIN only.
-- The ADMIN CROSS JOIN grant in V005 already ran, so grant these explicitly here.

INSERT INTO permission (code, name_th, name_en, category) VALUES
 ('user.read',   N'ดูผู้ใช้งานระบบ',   'View users',   'User'),
 ('user.manage', N'จัดการผู้ใช้งานระบบ', 'Manage users', 'User');

INSERT INTO role_permission (role_id, permission_code)
SELECT r.id, p.code FROM role r JOIN permission p ON p.code IN ('user.read', 'user.manage')
WHERE r.code = 'ADMIN';
