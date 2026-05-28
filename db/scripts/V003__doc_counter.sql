-- Per-(prefix, year) document-number counter. Replaces the COUNT-based generator,
-- which was not safe under concurrent requests. The application increments this
-- atomically (UPDATE ... WITH (UPDLOCK, SERIALIZABLE), inserting the row on first
-- use of a prefix/year) so numbers are race-free across instances AND reset each
-- year. The unique index on each *_no column remains the final backstop.

CREATE TABLE document_counter (
    prefix VARCHAR(10) NOT NULL,
    [year] INT NOT NULL,
    next_value BIGINT NOT NULL CONSTRAINT df_document_counter_next DEFAULT (1),
    CONSTRAINT pk_document_counter PRIMARY KEY (prefix, [year])
);
