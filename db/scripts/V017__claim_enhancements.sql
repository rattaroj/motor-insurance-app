-- Claim enhancements: repair-shop (garage) master, surveyor assignment, and damage photos.
-- claim is system-versioned; the new columns propagate to claim_history automatically.
-- Liquibase owns schema.

CREATE TABLE garage (
    id BIGINT IDENTITY(1,1) CONSTRAINT pk_garage PRIMARY KEY,
    name NVARCHAR(150) NOT NULL,
    phone NVARCHAR(30) NULL
);
CREATE UNIQUE INDEX ux_garage_name ON garage (name);

INSERT INTO garage (name, phone) VALUES
 (N'ศูนย์ซ่อมสีและตัวถัง กรุงเทพ', N'02-111-0001'),
 (N'อู่กลางมาตรฐาน รามคำแหง', N'02-111-0002'),
 (N'ศูนย์บริการ Toyota สุขุมวิท', N'02-111-0003');

ALTER TABLE claim ADD
    garage_id     BIGINT        NULL CONSTRAINT fk_claim_garage REFERENCES garage (id),
    surveyor_name NVARCHAR(150) NULL;

CREATE TABLE claim_photo (
    id BIGINT IDENTITY(1,1) CONSTRAINT pk_claim_photo PRIMARY KEY,
    claim_id BIGINT NOT NULL,
    image_path NVARCHAR(400) NOT NULL,
    created_at DATETIME2 NOT NULL CONSTRAINT df_claim_photo_created DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT fk_claim_photo_claim FOREIGN KEY (claim_id) REFERENCES claim (id)
);
CREATE INDEX ix_claim_photo_claim ON claim_photo (claim_id);
