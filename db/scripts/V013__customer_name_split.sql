-- Split the customer name into title + first/last and add date of birth.
-- full_name is kept (now derived from the parts on write) so existing search/display
-- keep working unchanged. Existing rows are backfilled by splitting full_name on the
-- first space (first token = first name, remainder = last name).

ALTER TABLE customer ADD
    title       NVARCHAR(20)  NULL,
    first_name  NVARCHAR(100) NULL,
    last_name   NVARCHAR(100) NULL,
    birth_date  DATE          NULL;

UPDATE customer SET
    first_name = CASE WHEN CHARINDEX(N' ', full_name) > 0
                      THEN LEFT(full_name, CHARINDEX(N' ', full_name) - 1)
                      ELSE full_name END,
    last_name  = CASE WHEN CHARINDEX(N' ', full_name) > 0
                      THEN LTRIM(SUBSTRING(full_name, CHARINDEX(N' ', full_name) + 1, 200))
                      ELSE N'' END;
