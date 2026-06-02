-- New permission: policy.endorse (ทำสลักหลังกรมธรรม์). The ADMIN CROSS JOIN grant in
-- V005 already ran, so a permission added later must be granted to existing roles here.
-- Granted to ADMIN and UNDERWRITER (the role that manages the policy lifecycle).

INSERT INTO permission (code, name_th, name_en, category) VALUES
 ('policy.endorse', N'ทำสลักหลังกรมธรรม์', 'Endorse policy', 'Policy');

INSERT INTO role_permission (role_id, permission_code)
SELECT r.id, 'policy.endorse' FROM role r WHERE r.code IN ('ADMIN', 'UNDERWRITER');
