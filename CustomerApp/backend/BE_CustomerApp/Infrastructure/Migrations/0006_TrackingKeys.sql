-- Location points sent by the phone's background tracking service (works while the app is closed).
-- The service authenticates with a per-session key instead of the agent's sign-in tokens; the key stops working
-- as soon as the working day (session) ends. Only its SHA-256 hash is stored.
ALTER TABLE agents.WorkSessions ADD TrackingKeyHash CHAR(64) NULL;
GO
CREATE UNIQUE INDEX UX_WorkSessions_TrackingKey ON agents.WorkSessions(TrackingKeyHash) WHERE TrackingKeyHash IS NOT NULL;
