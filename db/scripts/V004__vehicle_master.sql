-- Seed vehicle master data: brand -> model -> submodel -> model_year.
-- Years 2021-2025 are generated for every submodel.

INSERT INTO vehicle_brand (name) VALUES (N'Toyota'), (N'Honda'), (N'Mazda'), (N'Isuzu');

INSERT INTO vehicle_model (brand_id, name)
SELECT b.id, m.name
FROM (VALUES
    ('Toyota', N'Yaris'),
    ('Toyota', N'Corolla Altis'),
    ('Toyota', N'Camry'),
    ('Honda',  N'City'),
    ('Honda',  N'Civic'),
    ('Honda',  N'CR-V'),
    ('Mazda',  N'Mazda2'),
    ('Mazda',  N'CX-5'),
    ('Isuzu',  N'D-Max'),
    ('Isuzu',  N'MU-X')
) AS m(brand, name)
JOIN vehicle_brand b ON b.name = m.brand;

INSERT INTO vehicle_submodel (model_id, name)
SELECT mo.id, s.name
FROM (VALUES
    (N'Yaris',          N'Entry'),
    (N'Yaris',          N'Sport Premium'),
    (N'Corolla Altis',  N'1.6G'),
    (N'Corolla Altis',  N'1.8 Hybrid'),
    (N'Camry',          N'2.5G'),
    (N'Camry',          N'2.5 HEV Premium'),
    (N'City',           N'SV'),
    (N'City',           N'RS'),
    (N'Civic',          N'EL+'),
    (N'Civic',          N'RS'),
    (N'CR-V',           N'EL'),
    (N'Mazda2',         N'S'),
    (N'Mazda2',         N'SP'),
    (N'CX-5',           N'S'),
    (N'CX-5',           N'SP'),
    (N'D-Max',          N'Hi-Lander'),
    (N'D-Max',          N'V-Cross'),
    (N'MU-X',           N'Ultimate')
) AS s(model, name)
JOIN vehicle_model mo ON mo.name = s.model;

INSERT INTO vehicle_model_year (submodel_id, [year])
SELECT s.id, y.yr
FROM vehicle_submodel s
CROSS JOIN (VALUES (2021), (2022), (2023), (2024), (2025)) AS y(yr);
