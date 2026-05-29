-- Shared audit columns for every BaseEntity table (see Domain/Common/BaseEntity.cs):
--   created_user / updated_user : username from ICurrentUser, stamped by AppDbContext
--   updated_at                  : last-modified timestamp (created_at already exists)
--   is_active                   : soft-active flag, defaults to 1
-- policy and claim are system-versioned temporal tables: ALTER TABLE ADD/DROP COLUMN
-- propagates to their *_history tables automatically, so we only touch the main tables.
-- app_user already has is_active (V005), so it only gets the three new columns.

ALTER TABLE customer ADD
    created_user NVARCHAR(100) NULL,
    updated_user NVARCHAR(100) NULL,
    updated_at   DATETIME2 NULL,
    is_active    BIT NOT NULL CONSTRAINT df_customer_active DEFAULT (1);

ALTER TABLE vehicle ADD
    created_user NVARCHAR(100) NULL,
    updated_user NVARCHAR(100) NULL,
    updated_at   DATETIME2 NULL,
    is_active    BIT NOT NULL CONSTRAINT df_vehicle_active DEFAULT (1);

ALTER TABLE quotation ADD
    created_user NVARCHAR(100) NULL,
    updated_user NVARCHAR(100) NULL,
    updated_at   DATETIME2 NULL,
    is_active    BIT NOT NULL CONSTRAINT df_quotation_active DEFAULT (1);

ALTER TABLE policy ADD
    created_user NVARCHAR(100) NULL,
    updated_user NVARCHAR(100) NULL,
    updated_at   DATETIME2 NULL,
    is_active    BIT NOT NULL CONSTRAINT df_policy_active DEFAULT (1);

ALTER TABLE claim ADD
    created_user NVARCHAR(100) NULL,
    updated_user NVARCHAR(100) NULL,
    updated_at   DATETIME2 NULL,
    is_active    BIT NOT NULL CONSTRAINT df_claim_active DEFAULT (1);

ALTER TABLE payment ADD
    created_user NVARCHAR(100) NULL,
    updated_user NVARCHAR(100) NULL,
    updated_at   DATETIME2 NULL,
    is_active    BIT NOT NULL CONSTRAINT df_payment_active DEFAULT (1);

ALTER TABLE app_user ADD
    created_user NVARCHAR(100) NULL,
    updated_user NVARCHAR(100) NULL,
    updated_at   DATETIME2 NULL;

ALTER TABLE refresh_token ADD
    created_user NVARCHAR(100) NULL,
    updated_user NVARCHAR(100) NULL,
    updated_at   DATETIME2 NULL,
    is_active    BIT NOT NULL CONSTRAINT df_refresh_token_active DEFAULT (1);
