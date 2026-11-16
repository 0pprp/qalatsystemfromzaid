-- «الوقت المستغرق عند المحل»: minutes the agent spent at a pinned shop. Required by the app from 1.4.7; pins saved
-- before stay NULL.
ALTER TABLE agents.Prospects ADD MinutesSpent SMALLINT NULL CONSTRAINT CK_Prospects_MinutesSpent CHECK (MinutesSpent BETWEEN 1 AND 600);
