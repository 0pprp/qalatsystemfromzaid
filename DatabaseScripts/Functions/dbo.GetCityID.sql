CREATE FUNCTION [dbo].[GetCityID] (@DatabaseName NVARCHAR(255))
RETURNS INT
AS
BEGIN
    DECLARE @CityID INT;

    SELECT @CityID = DatabaseID
    FROM (VALUES
        (1,  N'DatabaseCompanyNajaf'),
        (2,  N'DatabaseCompanyBaghdadRosafa'),
        (3,  N'DatabaseCompanyBaghdadKarak'),
        (4,  N'DatabaseCompanyKarbala'),
        (5,  N'DatabaseCompanyBabil'),
        (6,  N'DatabaseCompanyDewania'),
        (7,  N'DatabaseCompanyKot'),
        (8,  N'DatabaseCompanyNasria'),
        (9,  N'DatabaseCompanyBasra'),
        (10, N'DatabaseCompanyMothana'),
        (11, N'DatabaseCompanyDeiala'),
        (12, N'DatabaseCompanyBasraAlanwar'),
        (13, N'DatabaseCompanyMusol'),
        (14, N'DatabaseCompanyKarkok'),
        (15, N'DatabaseCompanyRusafaAqeel'),
        (16, N'DatabaseCompanyMaysan'),
        (18, N'DatabaseCompanyKarakAqeel')
    ) AS DBList(DatabaseID, DatabaseName)
    WHERE DatabaseName = @DatabaseName;

    RETURN @CityID;
END;
