-- Editable notification templates (subject + body) per template key. Lets staff tweak the
-- wording of reminders without a deploy — same philosophy as the configurable premium_rate
-- table. Bodies use {{placeholder}} tokens substituted at send time; the reminder helpers keep
-- a hardcoded default as the fallback when a row is absent. Liquibase owns schema.
-- Also adds notification.manage permission (ADMIN only).

CREATE TABLE notification_template (
    id BIGINT IDENTITY(1,1) CONSTRAINT pk_notification_template PRIMARY KEY,
    template_key VARCHAR(40) NOT NULL,
    subject NVARCHAR(200) NOT NULL,
    body NVARCHAR(2000) NOT NULL
);
CREATE UNIQUE INDEX ux_notification_template_key ON notification_template (template_key);

-- Seed with the wording previously hardcoded in RenewalReminders / InstallmentReminders.
INSERT INTO notification_template (template_key, subject, body) VALUES
 ('renewal',
  N'แจ้งเตือนต่ออายุกรมธรรม์ {{policyNo}}',
  N'เรียน {{customerName}}
กรมธรรม์เลขที่ {{policyNo}} จะหมดอายุวันที่ {{expiryDate}} กรุณาติดต่อเจ้าหน้าที่เพื่อต่ออายุความคุ้มครอง
เบี้ยต่ออายุโดยประมาณ {{estimatedPremium}}'),
 ('installment',
  N'แจ้งเตือนชำระเบี้ยงวดที่ {{installmentSeq}} กรมธรรม์ {{policyNo}}',
  N'เรียน {{customerName}}
เบี้ยประกันงวดที่ {{installmentSeq}} ของกรมธรรม์เลขที่ {{policyNo}} จำนวน {{amount}} บาท ครบกำหนดชำระเมื่อวันที่ {{dueDate}} และยังไม่ได้รับชำระ
กรุณาชำระโดยเร็วเพื่อรักษาความคุ้มครองของกรมธรรม์');

INSERT INTO permission (code, name_th, name_en, category) VALUES
 ('notification.manage', N'จัดการเทมเพลตแจ้งเตือน', 'Manage notification templates', 'Notification');

-- notification.manage → ADMIN only.
INSERT INTO role_permission (role_id, permission_code)
SELECT r.id, 'notification.manage' FROM role r WHERE r.code = 'ADMIN';
