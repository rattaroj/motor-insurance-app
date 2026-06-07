-- Rate versioning + configurable factors. Effective-dates the premium_rate table, and adds
-- configurable age-loading bands and deductible-relief settings (previously hardcoded in
-- PremiumCalculator, which remains the fallback). Reuses the rating.read/rating.manage perms.
-- Liquibase owns schema. splitStatements=true: each statement runs in its own batch so later
-- statements can reference columns/tables created by earlier ones (SQL Server name resolution).

-- Effective-date the existing rate table and re-key it by (coverage, effective_date).
ALTER TABLE premium_rate ADD effective_date DATE NOT NULL
    CONSTRAINT df_premium_rate_eff DEFAULT ('2000-01-01');

DROP INDEX ux_premium_rate_coverage ON premium_rate;

CREATE UNIQUE INDEX ux_premium_rate_coverage_eff ON premium_rate (coverage_type, effective_date);

-- Configurable vehicle-age loading bands (inclusive max_age; NULL = open-ended top band).
CREATE TABLE age_loading_band (
    id BIGINT IDENTITY(1,1) CONSTRAINT pk_age_loading_band PRIMARY KEY,
    max_age INT NULL,
    surcharge DECIMAL(6,4) NOT NULL,
    effective_date DATE NOT NULL CONSTRAINT df_age_loading_band_eff DEFAULT ('2000-01-01')
);
CREATE INDEX ix_age_loading_band_eff ON age_loading_band (effective_date);

INSERT INTO age_loading_band (max_age, surcharge, effective_date) VALUES
 (5,    0.0000, '2000-01-01'),
 (10,   0.0500, '2000-01-01'),
 (NULL, 0.1000, '2000-01-01');

-- Tunable scalar rating settings (deductible relief rate + cap), keyed by code.
CREATE TABLE rating_setting (
    code VARCHAR(50) NOT NULL CONSTRAINT pk_rating_setting PRIMARY KEY,
    value DECIMAL(9,4) NOT NULL
);

INSERT INTO rating_setting (code, value) VALUES
 ('DEDUCTIBLE_RELIEF_RATE', 0.5000),
 ('DEDUCTIBLE_RELIEF_CAP',  0.2000);
