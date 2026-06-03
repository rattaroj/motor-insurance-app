-- Advanced premium rating: store the rating inputs/base premium on quotation & policy,
-- plus an add-on rider master and its many-to-many links. Liquibase owns schema.
-- premium (existing) now holds the NET premium; base_premium holds the pre-discount base.
-- Existing rows backfill to ncb=0/deductible=0/base_premium=0 — with those, net == old premium.

ALTER TABLE quotation ADD
    ncb_percent  INT           NOT NULL CONSTRAINT df_quotation_ncb        DEFAULT (0),
    deductible   DECIMAL(18,2) NOT NULL CONSTRAINT df_quotation_deductible DEFAULT (0),
    base_premium DECIMAL(18,2) NOT NULL CONSTRAINT df_quotation_base       DEFAULT (0);

-- policy is system-versioned; new columns propagate to policy_history automatically.
ALTER TABLE policy ADD
    ncb_percent  INT           NOT NULL CONSTRAINT df_policy_ncb        DEFAULT (0),
    deductible   DECIMAL(18,2) NOT NULL CONSTRAINT df_policy_deductible DEFAULT (0),
    base_premium DECIMAL(18,2) NOT NULL CONSTRAINT df_policy_base       DEFAULT (0);

-- Add-on rider (ความคุ้มครองเสริม) master — managed lookup, flat premium per rider.
CREATE TABLE rider (
    id BIGINT IDENTITY(1,1) CONSTRAINT pk_rider PRIMARY KEY,
    name NVARCHAR(100) NOT NULL,
    premium DECIMAL(18,2) NOT NULL
);
CREATE UNIQUE INDEX ux_rider_name ON rider (name);

INSERT INTO rider (name, premium) VALUES
 (N'อุบัติเหตุส่วนบุคคล (PA)', 1200),
 (N'ค่ารักษาพยาบาล', 800),
 (N'ประกันตัวผู้ขับขี่', 500),
 (N'ภัยน้ำท่วม', 1500);

-- Selected riders per quotation / policy (composite-key join tables).
CREATE TABLE quotation_rider (
    quotation_id BIGINT NOT NULL,
    rider_id     BIGINT NOT NULL,
    CONSTRAINT pk_quotation_rider PRIMARY KEY (quotation_id, rider_id),
    CONSTRAINT fk_qr_quotation FOREIGN KEY (quotation_id) REFERENCES quotation (id),
    CONSTRAINT fk_qr_rider     FOREIGN KEY (rider_id)     REFERENCES rider (id)
);

CREATE TABLE policy_rider (
    policy_id BIGINT NOT NULL,
    rider_id  BIGINT NOT NULL,
    CONSTRAINT pk_policy_rider PRIMARY KEY (policy_id, rider_id),
    CONSTRAINT fk_pr_policy FOREIGN KEY (policy_id) REFERENCES policy (id),
    CONSTRAINT fk_pr_rider  FOREIGN KEY (rider_id)  REFERENCES rider (id)
);
