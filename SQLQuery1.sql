USE master;
GO

-- 1. CLEANUP & CREATION
IF EXISTS (SELECT * FROM sys.databases WHERE name = 'StreamAtlasDB_v3')
BEGIN
    ALTER DATABASE StreamAtlasDB_v3 SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE StreamAtlasDB_v3;
END
GO

CREATE DATABASE StreamAtlasDB_v3;
GO
USE StreamAtlasDB_v3;
GO

-- 2. TABLES

-- Core User Data
CREATE TABLE Users (
    UserId INT IDENTITY(1,1) PRIMARY KEY,
    Username NVARCHAR(50) UNIQUE NOT NULL,
    CreatedAt DATETIME DEFAULT GETDATE()
);

-- Meta Data (Dictionaries)
CREATE TABLE Genres (
    GenreId INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(50) UNIQUE NOT NULL
);

CREATE TABLE Actors (
    ActorId INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(100) UNIQUE NOT NULL
);

-- Media Tables
CREATE TABLE Movies (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Title NVARCHAR(100), Description NVARCHAR(MAX), Rating DECIMAL(3, 1), ReleaseYear INT, DurationMins INT,
    Director NVARCHAR(100)
);

CREATE TABLE Series (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Title NVARCHAR(100), Description NVARCHAR(MAX), StartYear INT, EndYear INT, Seasons INT,
    Network NVARCHAR(100)
);

CREATE TABLE Games (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Title NVARCHAR(100), Description NVARCHAR(MAX), Rating DECIMAL(3, 1), ReleaseYear INT, Platform NVARCHAR(50),
    Developer NVARCHAR(100)
);

-- Relationships (Joins)
CREATE TABLE Media_Genres (
    LinkId INT IDENTITY(1,1) PRIMARY KEY,
    GenreId INT FOREIGN KEY REFERENCES Genres(GenreId),
    MediaId INT, MediaType NVARCHAR(10) -- 'Movie', 'Series', 'Game'
);

CREATE TABLE Media_Actors (
    LinkId INT IDENTITY(1,1) PRIMARY KEY,
    ActorId INT FOREIGN KEY REFERENCES Actors(ActorId),
    MediaId INT, MediaType NVARCHAR(10)
);

-- User Interaction
CREATE TABLE Wishlist (
    UserId INT FOREIGN KEY REFERENCES Users(UserId),
    MediaId INT, MediaType NVARCHAR(10),
    AddedDate DATETIME DEFAULT GETDATE(),
    PRIMARY KEY (UserId, MediaId, MediaType)
);

CREATE TABLE Reviews (
    ReviewId INT IDENTITY(1,1) PRIMARY KEY,
    UserId INT FOREIGN KEY REFERENCES Users(UserId),
    MediaId INT, MediaType NVARCHAR(10),
    Rating INT CHECK (Rating BETWEEN 1 AND 5), 
    Comment NVARCHAR(MAX), 
    ReviewDate DATETIME DEFAULT GETDATE()
);

-- 3. INDEXES (Optimization)
CREATE INDEX IX_Movies_Title ON Movies(Title);
CREATE INDEX IX_Series_Title ON Series(Title);
CREATE INDEX IX_Games_Title ON Games(Title);
CREATE INDEX IX_Actors_Name ON Actors(Name);

-- 4. VIEWS
GO
CREATE VIEW v_AllMedia AS
    SELECT Id, Title, Description, ReleaseYear, Rating, 
           CAST(DurationMins AS NVARCHAR(50)) + ' min' as ExtraInfo, 
           Director as Creator, 'Movie' as Type 
    FROM Movies
    UNION ALL
    SELECT Id, Title, Description, StartYear as ReleaseYear, 0 as Rating, 
           CAST(Seasons AS NVARCHAR(50)) + ' Seasons' as ExtraInfo, 
           Network as Creator, 'Series' as Type 
    FROM Series
    UNION ALL
    SELECT Id, Title, Description, ReleaseYear, Rating, 
           Platform as ExtraInfo, 
           Developer as Creator, 'Game' as Type 
    FROM Games;
GO

-- 5. SEED DATA

-- A. Genres (IDs 1-12)
INSERT INTO Genres (Name) VALUES 
('Action'), ('Sci-Fi'), ('Drama'), ('RPG'), ('Thriller'), 
('Adventure'), ('Crime'), ('Comedy'), ('Horror'), 
('Fantasy'), ('Animation'), ('Mystery');

-- B. Movies (IDs 1-15)
INSERT INTO Movies (Title, Description, Rating, ReleaseYear, DurationMins, Director) VALUES 
('Inception', 'A thief steals corporate secrets through dream-sharing technology.', 8.8, 2010, 148, 'Christopher Nolan'),
('Interstellar', 'A team of explorers travel through a wormhole in space.', 8.6, 2014, 169, 'Christopher Nolan'),
('The Dark Knight', 'Batman faces the Joker in Gotham City.', 9.0, 2008, 152, 'Christopher Nolan'),
('Pulp Fiction', 'The lives of two mob hitmen intertwine in four tales of violence.', 8.9, 1994, 154, 'Quentin Tarantino'),
('The Godfather', 'The aging patriarch of an organized crime dynasty transfers control to his reluctant son.', 9.2, 1972, 175, 'Francis Ford Coppola'),
('Fight Club', 'An insomniac office worker and a devil-may-care soapmaker form an underground fight club.', 8.8, 1999, 139, 'David Fincher'),
('Forrest Gump', 'The presidencies of Kennedy and Johnson, the Vietnam War, and more unfold through the perspective of an Alabama man.', 8.8, 1994, 142, 'Robert Zemeckis'),
('The Matrix', 'A computer hacker learns from mysterious rebels about the true nature of his reality.', 8.7, 1999, 136, 'Lana Wachowski'),
('Goodfellas', 'The story of Henry Hill and his life in the mob.', 8.7, 1990, 146, 'Martin Scorsese'),
('Se7en', 'Two detectives, a rookie and a veteran, hunt a serial killer.', 8.6, 1995, 127, 'David Fincher'),
('Silence of the Lambs', 'A young F.B.I. cadet must receive the help of an incarcerated and manipulative cannibal.', 8.6, 1991, 118, 'Jonathan Demme'),
('City of God', 'In the slums of Rio, two kids paths diverge as one struggles to become a photographer and the other a kingpin.', 8.6, 2002, 130, 'Fernando Meirelles'),
('Spirited Away', 'A sullen ten-year-old girl wanders into a world ruled by gods, witches, and spirits.', 8.6, 2001, 125, 'Hayao Miyazaki'),
('Parasite', 'Greed and class discrimination threaten the newly formed symbiotic relationship between the wealthy Park family and the destitute Kim clan.', 8.5, 2019, 132, 'Bong Joon Ho'),
('The Lion King', 'Lion prince Simba and his father are targeted by his bitter uncle.', 8.5, 1994, 88, 'Roger Allers');

-- C. Series (IDs 1-15)
INSERT INTO Series (Title, Description, StartYear, EndYear, Seasons, Network) VALUES 
('Breaking Bad', 'A high school chemistry teacher turns to crime.', 2008, 2013, 5, 'AMC'),
('Stranger Things', 'Supernatural mysteries in Hawkins.', 2016, 0, 4, 'Netflix'),
('Game of Thrones', 'Nine noble families fight for control over Westeros.', 2011, 2019, 8, 'HBO'),
('The Sopranos', 'New Jersey mob boss Tony Soprano deals with personal and professional issues.', 1999, 2007, 6, 'HBO'),
('The Wire', 'The Baltimore drug scene, as seen through the eyes of drug dealers and law enforcement.', 2002, 2008, 5, 'HBO'),
('Chernobyl', 'In April 1986, an explosion at the Chernobyl nuclear power plant becomes one of the world''s worst man-made catastrophes.', 2019, 2019, 1, 'HBO'),
('Friends', 'Follows the personal and professional lives of six twenty to thirty-something-year-old friends living in Manhattan.', 1994, 2004, 10, 'NBC'),
('The Office', 'A mockumentary on a group of typical office workers.', 2005, 2013, 9, 'NBC'),
('Sherlock', 'A modern update finds the famous sleuth and his doctor partner solving crime in 21st century London.', 2010, 2017, 4, 'BBC'),
('Black Mirror', 'An anthology series exploring a twisted, high-tech multiverse.', 2011, 0, 6, 'Netflix'),
('Peaky Blinders', 'A gangster family epic set in 1900s England.', 2013, 2022, 6, 'BBC'),
('Better Call Saul', 'The trials and tribulations of criminal lawyer Jimmy McGill.', 2015, 2022, 6, 'AMC'),
('Arcane', 'Set in Utopian Piltover and the oppressed underground of Zaun, the story follows the origins of two iconic League champions.', 2021, 0, 2, 'Netflix'),
('The Mandalorian', 'The travels of a lone bounty hunter in the outer reaches of the galaxy.', 2019, 0, 3, 'Disney+'),
('Succession', 'The Roy family is known for controlling the biggest media and entertainment company in the world.', 2018, 2023, 4, 'HBO');

-- D. Games (IDs 1-15)
INSERT INTO Games (Title, Description, Rating, ReleaseYear, Platform, Developer) VALUES 
('Elden Ring', 'Action RPG in the Lands Between.', 9.5, 2022, 'PC/PS5', 'FromSoftware'),
('God of War', 'Kratos journeys with his son Atreus.', 9.7, 2018, 'PS4', 'Santa Monica'),
('The Witcher 3', 'Geralt searches for Ciri.', 9.8, 2015, 'PC/Console', 'CDPR'),
('Red Dead Redemption 2', 'Epic tale of life in America’s unforgiving heartland.', 9.7, 2018, 'Multi', 'Rockstar'),
('The Last of Us Part I', 'A hardened survivor takes charge of a 14-year-old girl.', 9.8, 2013, 'PlayStation', 'Naughty Dog'),
('Grand Theft Auto V', 'Three very different criminals plot their own chances of survival and success.', 9.5, 2013, 'Multi', 'Rockstar'),
('Hades', 'Defy the god of the dead as you hack and slash out of the Underworld.', 9.3, 2020, 'PC/Switch', 'Supergiant'),
('Minecraft', 'Explore your own unique world, survive the night, and create anything you can imagine.', 9.0, 2011, 'Multi', 'Mojang'),
('Portal 2', 'Use the portal gun to solve puzzles.', 9.5, 2011, 'PC/Console', 'Valve'),
('BioShock', 'A plane crash survivor discovers the underwater city of Rapture.', 9.6, 2007, 'PC/Console', '2K Boston'),
('Mass Effect 2', 'Commander Shepard must assemble a new team to save humanity.', 9.5, 2010, 'Multi', 'BioWare'),
('Dark Souls', 'Action RPG known for its high difficulty.', 9.0, 2011, 'Multi', 'FromSoftware'),
('The Legend of Zelda: BOTW', 'Link wakes up from a 100-year slumber to defeat Calamity Ganon.', 9.7, 2017, 'Switch', 'Nintendo'),
('Cyberpunk 2077', 'An open-world, action-adventure story set in Night City.', 8.6, 2020, 'Multi', 'CDPR'),
('Hollow Knight', 'Forge your own path in a vast ruined kingdom of insects and heroes.', 9.4, 2017, 'Multi', 'Team Cherry');

-- E. Actors (Selected for Search)
INSERT INTO Actors (Name) VALUES 
('Leonardo DiCaprio'), ('Christian Bale'), ('Bryan Cranston'), -- 1-3
('Al Pacino'), ('Brad Pitt'), ('Tom Hanks'), ('Keanu Reeves'), -- 4-7
('Robert De Niro'), ('Jodie Foster'), ('Pedro Pascal'); -- 8-10

-- F. Linking Genres (Sample linking)
INSERT INTO Media_Genres (GenreId, MediaId, MediaType) VALUES 
(2, 1, 'Movie'), (1, 1, 'Movie'), -- Inception (SciFi, Action)
(2, 2, 'Movie'), -- Interstellar (SciFi)
(1, 3, 'Movie'), (7, 3, 'Movie'), -- Dark Knight (Action, Crime)
(7, 4, 'Movie'), (3, 4, 'Movie'), -- Pulp Fiction (Crime, Drama)
(7, 5, 'Movie'), (3, 5, 'Movie'), -- Godfather
(3, 6, 'Movie'), -- Fight Club
(3, 7, 'Movie'), (8, 7, 'Movie'), -- Forrest Gump (Drama, Comedy)
(2, 8, 'Movie'), (1, 8, 'Movie'), -- Matrix
(7, 1, 'Series'), (3, 1, 'Series'), -- Breaking Bad (Crime, Drama)
(9, 2, 'Series'), (2, 2, 'Series'), -- Stranger Things (Horror, SciFi)
(10, 3, 'Series'), (1, 3, 'Series'), -- GoT (Fantasy, Action)
(4, 1, 'Game'), (6, 1, 'Game'), -- Elden Ring (RPG, Adventure)
(6, 2, 'Game'), (1, 2, 'Game'), -- God of War
(4, 3, 'Game'); -- Witcher 3

-- G. Linking Actors (Sample linking)
INSERT INTO Media_Actors (ActorId, MediaId, MediaType) VALUES
(1, 1, 'Movie'), -- Leo in Inception
(2, 3, 'Movie'), -- Bale in Dark Knight
(3, 1, 'Series'), -- Cranston in Breaking Bad
(4, 5, 'Movie'), -- Al Pacino in Godfather
(5, 6, 'Movie'), -- Brad Pitt in Fight Club
(5, 10, 'Movie'), -- Brad Pitt in Se7en
(6, 7, 'Movie'), -- Tom Hanks in Forrest Gump
(7, 8, 'Movie'), -- Keanu Reeves in Matrix
(8, 5, 'Movie'), -- De Niro in Godfather (Part II mainly, but keeping it simple)
(8, 9, 'Movie'), -- De Niro in Goodfellas
(9, 11, 'Movie'), -- Jodie Foster in Silence of the Lambs
(10, 14, 'Series'), -- Pedro Pascal in Mandalorian
(10, 5, 'Game'); -- Pedro Pascal in Last of Us (TV show link, loosely connected for demo)

Select * from Wishlist;




--Use this to find Genres that have no movies assigned to them yet
SELECT 
    g.Name AS GenreName, 
    mg.MediaId
FROM Genres g                -- Left Table (The "Primary" focus)
LEFT JOIN Media_Genres mg    -- Right Table
    ON g.GenreId = mg.GenreId;


-- opposite of left join
    SELECT 
    mg.MediaId, 
    g.Name AS GenreName
FROM Media_Genres mg          -- Left Table
RIGHT JOIN Genres g           -- Right Table (The "Primary" focus)
    ON mg.GenreId = g.GenreId;



-- Show all Movies and their associated Genres
SELECT 
    m.Title, 
    m.ReleaseYear, 
    g.Name AS Genre
FROM Movies m
INNER JOIN Media_Genres mg 
    ON m.Id = mg.MediaId AND mg.MediaType = 'Movie' -- Crucial check!
INNER JOIN Genres g 
    ON mg.GenreId = g.GenreId
ORDER BY m.Title;


-- Average Rating of all Movies
SELECT AVG(Rating) AS AverageMovieRating FROM Movies;

-- The Longest Movie Duration
SELECT MAX(DurationMins) AS LongestRuntime FROM Movies;

-- Total number of Games per Platform
SELECT Platform, COUNT(*) AS GameCount
FROM Games
GROUP BY Platform;


--Who are the top Directors based on how many movies they have in the database
SELECT 
    Director, 
    COUNT(Id) AS MovieCount,
    AVG(Rating) AS AverageRating
FROM Movies
GROUP BY Director
ORDER BY MovieCount DESC, AverageRating DESC;


-- Shows Series Networks that have more than 2 shows in the database.
SELECT 
    Network, 
    COUNT(Id) AS NumberOfShows,
    SUM(Seasons) AS TotalSeasonsAvailable
FROM Series
GROUP BY Network
HAVING COUNT(Id) > 2;


