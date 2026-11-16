-- Photos and documents in the withdrawal chat (both directions). Files are private (Receipts:Root/chat/…), served only
-- to the dashboard and to the promoter who owns the request.
ALTER TABLE promoters.PayoutMessages ADD
    AttachmentPath        VARCHAR(400)  NULL,
    AttachmentName        NVARCHAR(200) NULL,
    AttachmentKind        VARCHAR(10)   NULL,   -- image | file
    AttachmentContentType VARCHAR(100)  NULL,
    AttachmentSize        BIGINT        NULL;
