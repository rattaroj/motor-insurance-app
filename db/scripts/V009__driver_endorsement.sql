-- Named drivers on a quotation (new-law requirement, max 5 enforced in the app) and
-- policy endorsements (สลักหลัง). Both inherit the BaseEntity audit columns (see V008).
-- Liquibase owns schema.

CREATE TABLE quotation_driver (
    id BIGINT IDENTITY(1,1) CONSTRAINT pk_quotation_driver PRIMARY KEY,
    quotation_id BIGINT NOT NULL,
    full_name NVARCHAR(200) NOT NULL,
    national_id VARCHAR(13) NOT NULL,
    id_card_image_path NVARCHAR(400) NOT NULL,
    created_user NVARCHAR(100) NULL,
    updated_user NVARCHAR(100) NULL,
    created_at DATETIME2 NOT NULL CONSTRAINT df_quotation_driver_created DEFAULT (SYSUTCDATETIME()),
    updated_at DATETIME2 NULL,
    is_active BIT NOT NULL CONSTRAINT df_quotation_driver_active DEFAULT (1),
    CONSTRAINT fk_quotation_driver_quotation FOREIGN KEY (quotation_id)
        REFERENCES quotation (id) ON DELETE CASCADE
);
CREATE INDEX ix_quotation_driver_quotation ON quotation_driver (quotation_id);

CREATE TABLE endorsement (
    id BIGINT IDENTITY(1,1) CONSTRAINT pk_endorsement PRIMARY KEY,
    endorsement_no VARCHAR(30) NOT NULL,
    policy_id BIGINT NOT NULL,
    field_name VARCHAR(50) NOT NULL,
    old_value NVARCHAR(500) NULL,
    new_value NVARCHAR(500) NULL,
    effective_date DATE NOT NULL,
    note NVARCHAR(500) NULL,
    created_user NVARCHAR(100) NULL,
    updated_user NVARCHAR(100) NULL,
    created_at DATETIME2 NOT NULL CONSTRAINT df_endorsement_created DEFAULT (SYSUTCDATETIME()),
    updated_at DATETIME2 NULL,
    is_active BIT NOT NULL CONSTRAINT df_endorsement_active DEFAULT (1),
    CONSTRAINT fk_endorsement_policy FOREIGN KEY (policy_id) REFERENCES policy (id)
);
CREATE UNIQUE INDEX ux_endorsement_no ON endorsement (endorsement_no);
CREATE INDEX ix_endorsement_policy ON endorsement (policy_id);
