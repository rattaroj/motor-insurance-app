-- New permission: notification.read (ดูประวัติการแจ้งเตือน). The ADMIN CROSS JOIN grant in
-- V005 already ran, so a permission added later must be granted to existing roles here.
-- Read-only auditing surface — granted to every role that can view the dashboard.

INSERT INTO permission (code, name_th, name_en, category) VALUES
 ('notification.read', N'ดูประวัติการแจ้งเตือน', 'View notifications', 'Notification');

INSERT INTO role_permission (role_id, permission_code)
SELECT r.id, 'notification.read' FROM role r
 WHERE r.code IN ('ADMIN', 'UNDERWRITER', 'CLAIMS', 'FINANCE', 'VIEWER');
