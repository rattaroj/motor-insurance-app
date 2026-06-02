-- Customer address: optional references to the Thai administrative-division master
-- (province -> district -> subdistrict + postal code) plus a free-text line for
-- house no./moo/road. All nullable: existing customers have no address on file.

ALTER TABLE customer ADD
    address_line   NVARCHAR(255) NULL,
    province_id    BIGINT NULL,
    district_id    BIGINT NULL,
    subdistrict_id BIGINT NULL,
    postal_code_id BIGINT NULL;

ALTER TABLE customer ADD CONSTRAINT fk_customer_province    FOREIGN KEY (province_id)    REFERENCES province (id);
ALTER TABLE customer ADD CONSTRAINT fk_customer_district    FOREIGN KEY (district_id)    REFERENCES district (id);
ALTER TABLE customer ADD CONSTRAINT fk_customer_subdistrict FOREIGN KEY (subdistrict_id) REFERENCES subdistrict (id);
ALTER TABLE customer ADD CONSTRAINT fk_customer_postal_code FOREIGN KEY (postal_code_id) REFERENCES postal_code (id);
