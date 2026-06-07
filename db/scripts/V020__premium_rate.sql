-- Configurable base-premium rate per coverage type (พิกัดอัตราเบี้ย). One row per coverage
-- type; rate × sum insured = base premium. Replaces the hardcoded rates in PremiumCalculator
-- (which remains the fallback when a row is absent). Liquibase owns schema.
-- Also adds rating.read / rating.manage permissions.

CREATE TABLE premium_rate (
    id BIGINT IDENTITY(1,1) CONSTRAINT pk_premium_rate PRIMARY KEY,
    coverage_type VARCHAR(20) NOT NULL,
    rate DECIMAL(6,4) NOT NULL,
    CONSTRAINT ck_premium_rate_rate CHECK (rate > 0 AND rate <= 1)
);
CREATE UNIQUE INDEX ux_premium_rate_coverage ON premium_rate (coverage_type);

-- Seed with the values previously hardcoded in PremiumCalculator.
INSERT INTO premium_rate (coverage_type, rate) VALUES
 ('TYPE1',     0.0450),
 ('TYPE2PLUS', 0.0300),
 ('TYPE3PLUS', 0.0220),
 ('TYPE3',     0.0150);

INSERT INTO permission (code, name_th, name_en, category) VALUES
 ('rating.read',   N'ดูพิกัดอัตราเบี้ย',   'View premium rates',   'Rating'),
 ('rating.manage', N'จัดการพิกัดอัตราเบี้ย', 'Manage premium rates', 'Rating');

-- rating.read → ADMIN + UNDERWRITER (who quote); rating.manage → ADMIN only.
INSERT INTO role_permission (role_id, permission_code)
SELECT r.id, 'rating.read' FROM role r WHERE r.code IN ('ADMIN', 'UNDERWRITER');
INSERT INTO role_permission (role_id, permission_code)
SELECT r.id, 'rating.manage' FROM role r WHERE r.code = 'ADMIN';
