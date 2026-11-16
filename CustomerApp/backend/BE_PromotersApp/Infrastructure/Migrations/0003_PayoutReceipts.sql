-- «تم التحويل» needs a picture of the transfer receipt. The file is stored outside the public media folder
-- (Receipts:Root) and served only to the dashboard and to the promoter who made the request.
ALTER TABLE promoters.Payouts ADD ReceiptPath VARCHAR(400) NULL, ReceiptContentType VARCHAR(50) NULL;
