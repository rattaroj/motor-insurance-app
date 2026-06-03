-- Customer title (คำนำหน้าชื่อ) master table. Promotes the previously hard-coded
-- title option list to a managed lookup. customer.title stays a free string (it is
-- composed into full_name); this table only governs the allowed options. Liquibase owns schema.

CREATE TABLE customer_title (
    id BIGINT IDENTITY(1,1) CONSTRAINT pk_customer_title PRIMARY KEY,
    name NVARCHAR(20) NOT NULL
);
CREATE UNIQUE INDEX ux_customer_title_name ON customer_title (name);

INSERT INTO customer_title (name) VALUES (N'นาย'), (N'นาง'), (N'นางสาว');
