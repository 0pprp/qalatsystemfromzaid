CREATE FUNCTION [dbo].[GetCityID] (@DatabaseName NVARCHAR(255))
RETURNS INT
AS
BEGIN
    DECLARE @CityID INT;

    SELECT @CityID = DatabaseID
    FROM (VALUES
        (1, 'DatabaseCompanyNajaf'),
        (2, 'DatabaseCompanyBaghdadKarak'),
        (2, 'DatabaseCompanyBaghdadRosafa'),
        (3, 'DatabaseCompanyKarbala'),
        (4, 'DatabaseCompanyBabil'),
        (5, 'DatabaseCompanyDewania'),
        (6, 'DatabaseCompanyKot'),
        (7, 'DatabaseCompanyNasria'),
        (8, 'DatabaseCompanyBasra'),
        (9, 'DatabaseCompanyMothana'),
        (10, 'DatabaseCompanyDeiala'),
        (11, 'DatabaseCompanySulaymaniyah'),
        (12, 'DatabaseCompanyMusol'),
        (13, 'DatabaseCompanyAnbar'),
        (14, 'DatabaseCompanySalahaddin'),
        (15, 'DatabaseCompanyKarkok'),
        (16, 'DatabaseCompanyNineveh'),
        (17, 'DatabaseCompanyDohuk'),
        (18, 'DatabaseCompanyMaysan')
    ) AS DBList(DatabaseID, DatabaseName)
    WHERE DatabaseName = @DatabaseName;

    RETURN @CityID;
END;
 

