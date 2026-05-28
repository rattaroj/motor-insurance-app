-- Core domain tables (SQL Server). policy/claim are system-versioned (temporal).

CREATE TABLE customer (
    id BIGINT IDENTITY(1,1) CONSTRAINT pk_customer PRIMARY KEY,
    national_id VARCHAR(13) NOT NULL,
    full_name NVARCHAR(200) NOT NULL,
    phone VARCHAR(20) NULL, email VARCHAR(255) NULL,
    created_at DATETIME2 NOT NULL CONSTRAINT df_customer_created DEFAULT (SYSUTCDATETIME())
);
CREATE UNIQUE INDEX ux_customer_national_id ON customer (national_id);

-- Vehicle master data: brand -> model -> submodel -> model_year (cascading lookups)
CREATE TABLE vehicle_brand (
    id BIGINT IDENTITY(1,1) CONSTRAINT pk_vehicle_brand PRIMARY KEY,
    name NVARCHAR(50) NOT NULL
);
CREATE UNIQUE INDEX ux_vehicle_brand_name ON vehicle_brand (name);

CREATE TABLE vehicle_model (
    id BIGINT IDENTITY(1,1) CONSTRAINT pk_vehicle_model PRIMARY KEY,
    brand_id BIGINT NOT NULL,
    name NVARCHAR(50) NOT NULL,
    CONSTRAINT fk_vehicle_model_brand FOREIGN KEY (brand_id) REFERENCES vehicle_brand (id)
);
CREATE UNIQUE INDEX ux_vehicle_model_brand_name ON vehicle_model (brand_id, name);

CREATE TABLE vehicle_submodel (
    id BIGINT IDENTITY(1,1) CONSTRAINT pk_vehicle_submodel PRIMARY KEY,
    model_id BIGINT NOT NULL,
    name NVARCHAR(50) NOT NULL,
    CONSTRAINT fk_vehicle_submodel_model FOREIGN KEY (model_id) REFERENCES vehicle_model (id)
);
CREATE UNIQUE INDEX ux_vehicle_submodel_model_name ON vehicle_submodel (model_id, name);

CREATE TABLE vehicle_model_year (
    id BIGINT IDENTITY(1,1) CONSTRAINT pk_vehicle_model_year PRIMARY KEY,
    submodel_id BIGINT NOT NULL,
    [year] INT NOT NULL,
    CONSTRAINT fk_vehicle_model_year_submodel FOREIGN KEY (submodel_id) REFERENCES vehicle_submodel (id)
);
CREATE UNIQUE INDEX ux_vehicle_model_year ON vehicle_model_year (submodel_id, [year]);

CREATE TABLE vehicle (
    id BIGINT IDENTITY(1,1) CONSTRAINT pk_vehicle PRIMARY KEY,
    customer_id BIGINT NOT NULL,
    registration_no NVARCHAR(20) NOT NULL, province NVARCHAR(50) NOT NULL,
    model_year_id BIGINT NOT NULL,
    chassis_no VARCHAR(50) NULL,
    created_at DATETIME2 NOT NULL CONSTRAINT df_vehicle_created DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT fk_vehicle_customer FOREIGN KEY (customer_id) REFERENCES customer (id),
    CONSTRAINT fk_vehicle_model_year FOREIGN KEY (model_year_id) REFERENCES vehicle_model_year (id)
);
CREATE UNIQUE INDEX ux_vehicle_reg ON vehicle (registration_no, province);

CREATE TABLE quotation (
    id BIGINT IDENTITY(1,1) CONSTRAINT pk_quotation PRIMARY KEY,
    quotation_no VARCHAR(30) NOT NULL,
    customer_id BIGINT NOT NULL, vehicle_id BIGINT NOT NULL,
    coverage_type VARCHAR(20) NOT NULL,
    sum_insured DECIMAL(18,2) NOT NULL, premium DECIMAL(18,2) NOT NULL,
    valid_until DATE NOT NULL,
    created_at DATETIME2 NOT NULL CONSTRAINT df_quotation_created DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT fk_quotation_customer FOREIGN KEY (customer_id) REFERENCES customer (id),
    CONSTRAINT fk_quotation_vehicle FOREIGN KEY (vehicle_id) REFERENCES vehicle (id)
);
CREATE UNIQUE INDEX ux_quotation_no ON quotation (quotation_no);

CREATE TABLE policy (
    id BIGINT IDENTITY(1,1) CONSTRAINT pk_policy PRIMARY KEY,
    policy_no VARCHAR(30) NOT NULL,
    quotation_id BIGINT NULL, customer_id BIGINT NOT NULL, vehicle_id BIGINT NOT NULL,
    status_code VARCHAR(20) NOT NULL, coverage_type VARCHAR(20) NOT NULL,
    sum_insured DECIMAL(18,2) NOT NULL, premium DECIMAL(18,2) NOT NULL,
    effective_date DATE NULL, expiry_date DATE NULL,
    previous_policy_id BIGINT NULL,            -- renewal link
    row_version ROWVERSION NOT NULL,
    created_at DATETIME2 NOT NULL CONSTRAINT df_policy_created DEFAULT (SYSUTCDATETIME()),
    valid_from DATETIME2 GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
    valid_to DATETIME2 GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,
    PERIOD FOR SYSTEM_TIME (valid_from, valid_to),
    CONSTRAINT fk_policy_status FOREIGN KEY (status_code) REFERENCES policy_status (code),
    CONSTRAINT fk_policy_quotation FOREIGN KEY (quotation_id) REFERENCES quotation (id),
    CONSTRAINT fk_policy_customer FOREIGN KEY (customer_id) REFERENCES customer (id),
    CONSTRAINT fk_policy_vehicle FOREIGN KEY (vehicle_id) REFERENCES vehicle (id),
    CONSTRAINT fk_policy_prev FOREIGN KEY (previous_policy_id) REFERENCES policy (id)
) WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = dbo.policy_history));
CREATE UNIQUE INDEX ux_policy_no ON policy (policy_no);
CREATE INDEX ix_policy_status ON policy (status_code);

CREATE TABLE claim (
    id BIGINT IDENTITY(1,1) CONSTRAINT pk_claim PRIMARY KEY,
    claim_no VARCHAR(30) NOT NULL, policy_id BIGINT NOT NULL,
    status_code VARCHAR(20) NOT NULL, incident_date DATE NOT NULL,
    description NVARCHAR(1000) NULL,
    claimed_amount DECIMAL(18,2) NOT NULL, approved_amount DECIMAL(18,2) NULL,
    reject_reason NVARCHAR(500) NULL,
    row_version ROWVERSION NOT NULL,
    created_at DATETIME2 NOT NULL CONSTRAINT df_claim_created DEFAULT (SYSUTCDATETIME()),
    valid_from DATETIME2 GENERATED ALWAYS AS ROW START HIDDEN NOT NULL,
    valid_to DATETIME2 GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,
    PERIOD FOR SYSTEM_TIME (valid_from, valid_to),
    CONSTRAINT fk_claim_status FOREIGN KEY (status_code) REFERENCES claim_status (code),
    CONSTRAINT fk_claim_policy FOREIGN KEY (policy_id) REFERENCES policy (id)
) WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = dbo.claim_history));
CREATE UNIQUE INDEX ux_claim_no ON claim (claim_no);
CREATE INDEX ix_claim_policy ON claim (policy_id);

CREATE TABLE payment (
    id BIGINT IDENTITY(1,1) CONSTRAINT pk_payment PRIMARY KEY,
    payment_no VARCHAR(30) NOT NULL,
    direction_code VARCHAR(20) NOT NULL, status_code VARCHAR(20) NOT NULL,
    policy_id BIGINT NULL, claim_id BIGINT NULL,
    amount DECIMAL(18,2) NOT NULL, paid_at DATETIME2 NULL, reference_no VARCHAR(100) NULL,
    row_version ROWVERSION NOT NULL,
    created_at DATETIME2 NOT NULL CONSTRAINT df_payment_created DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT fk_payment_status FOREIGN KEY (status_code) REFERENCES payment_status (code),
    CONSTRAINT fk_payment_policy FOREIGN KEY (policy_id) REFERENCES policy (id),
    CONSTRAINT fk_payment_claim FOREIGN KEY (claim_id) REFERENCES claim (id),
    CONSTRAINT ck_payment_target CHECK (policy_id IS NOT NULL OR claim_id IS NOT NULL)
);
CREATE UNIQUE INDEX ux_payment_no ON payment (payment_no);
