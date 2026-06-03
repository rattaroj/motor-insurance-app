-- Notification log: a persisted, auditable record of reminders sent (e.g. renewal reminders).
-- Delivery channel is pluggable (Email/Sms/Line/Log); the default dev sender records + logs.
-- Liquibase owns schema.

CREATE TABLE notification (
    id BIGINT IDENTITY(1,1) CONSTRAINT pk_notification PRIMARY KEY,
    policy_id BIGINT NULL,
    channel VARCHAR(20) NOT NULL,
    recipient NVARCHAR(200) NOT NULL,
    subject NVARCHAR(200) NOT NULL,
    body NVARCHAR(2000) NOT NULL,
    status VARCHAR(20) NOT NULL,
    sent_at DATETIME2 NULL,
    created_at DATETIME2 NOT NULL CONSTRAINT df_notification_created DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT fk_notification_policy FOREIGN KEY (policy_id) REFERENCES policy (id)
);
CREATE INDEX ix_notification_policy ON notification (policy_id);
