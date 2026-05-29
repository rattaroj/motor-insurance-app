-- Drop the dead `is_terminal` column from the status lookup tables.
-- It was seeded in V001 but never read by any code: there are no EF mappings for
-- policy_status / claim_status, and the lifecycle "terminal state" logic lives entirely
-- in Domain/StateMachines/StateMachines.cs (EnsureTransition). The lookup tables
-- themselves stay — they back the FK constraints on status_code (fk_policy_status,
-- fk_claim_status). We only remove the unused column (and its DEFAULT constraint, which
-- SQL Server requires dropping before the column).

ALTER TABLE policy_status DROP CONSTRAINT df_ps_term;
ALTER TABLE policy_status DROP COLUMN is_terminal;

ALTER TABLE claim_status DROP CONSTRAINT df_cs_term;
ALTER TABLE claim_status DROP COLUMN is_terminal;
