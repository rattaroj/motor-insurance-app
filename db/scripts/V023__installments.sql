-- Premium installment plans: pay the annual premium over N scheduled inbound payments.
-- Each installment is a row in the existing `payment` table (reusing the SettlePayment hub),
-- tagged with its plan, sequence and due date.

-- New non-terminal policy status for installment dunning (overdue → Suspended → reactivate on pay).
INSERT INTO policy_status (code, name_th, name_en, is_terminal, sort_order)
VALUES ('Suspended', N'ระงับชั่วคราว', 'Suspended', 0, 7);

-- Plan header. status: Active (paying) / Completed (all paid) / Defaulted (overdue, suspended).
CREATE TABLE installment_plan (
    id              BIGINT IDENTITY(1,1) CONSTRAINT pk_installment_plan PRIMARY KEY,
    policy_id       BIGINT NOT NULL,
    total_premium   DECIMAL(18,2) NOT NULL,   -- annual premium financed (excludes the fee)
    fee             DECIMAL(18,2) NOT NULL CONSTRAINT df_installment_fee DEFAULT (0),
    installments    INT NOT NULL,
    frequency_days  INT NOT NULL,
    status          VARCHAR(20) NOT NULL,
    created_user    NVARCHAR(100) NULL,
    created_at      DATETIME2 NOT NULL CONSTRAINT df_installment_created DEFAULT (SYSUTCDATETIME()),
    updated_user    NVARCHAR(100) NULL,
    updated_at      DATETIME2 NULL,
    is_active       BIT NOT NULL CONSTRAINT df_installment_active DEFAULT (1),
    CONSTRAINT fk_installment_plan_policy FOREIGN KEY (policy_id) REFERENCES policy (id),
    CONSTRAINT ck_installment_plan_status CHECK (status IN ('Active', 'Completed', 'Defaulted'))
);
CREATE INDEX ix_installment_plan_policy ON installment_plan (policy_id);

-- Tag payments that are installments of a plan.
ALTER TABLE payment ADD
    installment_plan_id BIGINT NULL,
    installment_seq     INT NULL,
    due_date            DATE NULL;

ALTER TABLE payment ADD CONSTRAINT fk_payment_installment_plan
    FOREIGN KEY (installment_plan_id) REFERENCES installment_plan (id);
