-- Usernames may be Arabic: the column becomes Unicode (it was VARCHAR, which cannot hold Arabic letters).
DROP INDEX UX_Users_Username ON agents.Users;
ALTER TABLE agents.Users ALTER COLUMN Username NVARCHAR(50) NULL;
CREATE UNIQUE INDEX UX_Users_Username ON agents.Users(Username) WHERE Username IS NOT NULL;
