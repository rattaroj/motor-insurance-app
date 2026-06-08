-- Per-customer LINE target for the LINE notification channel (Messaging API push).
-- Nullable: only customers who opted in / linked their LINE have a user id.
ALTER TABLE customer ADD line_user_id NVARCHAR(64) NULL;
