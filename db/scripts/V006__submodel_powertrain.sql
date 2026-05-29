-- Add powertrain (fuel type) to each vehicle submodel: GASOLINE / DIESEL / ELECTRIC / HYBRID.
-- These string values MUST match the Powertrain enum<->string converter in
-- Infrastructure/Persistence/Configurations/Configurations.cs (PowertrainConverter).
-- Run with splitStatements="true" (see changelog): the backfill UPDATEs reference the
-- column added above, which SQL Server cannot resolve within a single batch.

-- Add the column. A temporary default lets the NOT NULL apply to existing rows; we drop it
-- afterwards so the application must always supply a value explicitly (matches coverage_type).
ALTER TABLE vehicle_submodel
    ADD powertrain VARCHAR(20) NOT NULL
        CONSTRAINT df_vehicle_submodel_powertrain DEFAULT ('GASOLINE');

ALTER TABLE vehicle_submodel
    ADD CONSTRAINT ck_vehicle_submodel_powertrain
        CHECK (powertrain IN ('GASOLINE', 'DIESEL', 'ELECTRIC', 'HYBRID'));

-- Backfill the seeded data from what the submodel names / brands imply.
UPDATE vehicle_submodel
    SET powertrain = 'HYBRID'
    WHERE name LIKE '%Hybrid%' OR name LIKE '%HEV%';

UPDATE s
    SET s.powertrain = 'DIESEL'
    FROM vehicle_submodel s
    JOIN vehicle_model m ON m.id = s.model_id
    WHERE m.name IN (N'D-Max', N'MU-X');

-- New rows must specify a value via the app; drop the convenience default.
ALTER TABLE vehicle_submodel DROP CONSTRAINT df_vehicle_submodel_powertrain;
