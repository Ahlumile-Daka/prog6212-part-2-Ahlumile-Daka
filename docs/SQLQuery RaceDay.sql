-- RaceDay_Database.sql
-- Drop if exists (for clean re-runs) then create

-- Create dbo.Users only if it does not already exist to avoid 'object already exists' errors
IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Users (
        UserID          INT IDENTITY(1,1) PRIMARY KEY,
        FirstName       NVARCHAR(50)  NOT NULL,
        LastName        NVARCHAR(50)  NOT NULL,
        Email           NVARCHAR(100) NOT NULL UNIQUE,
        PasswordHash    NVARCHAR(255) NOT NULL,
        Role            NVARCHAR(20)  NOT NULL CHECK (Role IN ('Organiser','Participant')),
        Phone           NVARCHAR(20)  NULL,
        DateRegistered  DATETIME2     NOT NULL DEFAULT GETDATE(),
        IsActive        BIT           NOT NULL DEFAULT 1
    );
END
ELSE
BEGIN
    PRINT N'Table dbo.Users already exists - creation skipped.';
END

CREATE TABLE dbo.TypeEvent (
    EventTypeID INT IDENTITY(1,1) PRIMARY KEY,
    Name        NVARCHAR(30) NOT NULL UNIQUE,
    Description NVARCHAR(200) NULL
);

CREATE TABLE dbo.EventDay (
    EventID         INT IDENTITY(1,1) PRIMARY KEY,
    EventName       NVARCHAR(100) NOT NULL,
    Description     NVARCHAR(500) NULL,
    EventDate       DATETIME2     NOT NULL,
    Location        NVARCHAR(150) NOT NULL,
    DistanceKm      DECIMAL(6,2)  NOT NULL,
    EventTypeID     INT           NOT NULL,
    OrganiserID     INT           NOT NULL,
    MaxParticipants INT           NULL,
    Status          NVARCHAR(20)  NOT NULL DEFAULT 'Open',
    CreatedDate     DATETIME2     NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_Events_EventTypes FOREIGN KEY (EventTypeID) REFERENCES dbo.EventTypes(EventTypeID),
    CONSTRAINT FK_Events_Organiser FOREIGN KEY (OrganiserID) REFERENCES dbo.Users(UserID)
);

CREATE TABLE dbo.Categorie (
    CategoryID   INT IDENTITY(1,1) PRIMARY KEY,
    EventID      INT           NOT NULL,
    CategoryName NVARCHAR(50)  NOT NULL,
    MinAge       INT           NULL,
    MaxAge       INT           NULL,
    EntryFee     DECIMAL(8,2)  NOT NULL DEFAULT 0,
    MaxEntries   INT           NULL,
    CONSTRAINT FK_Categories_Events FOREIGN KEY (EventID) REFERENCES dbo.Events(EventID)
);

CREATE TABLE dbo.EnrolmentRegister (
    EnrolmentID    INT IDENTITY(1,1) PRIMARY KEY,
    ParticipantID  INT           NOT NULL,
    EventID        INT           NOT NULL,
    CategoryID     INT           NOT NULL,
    EnrolmentDate  DATETIME2     NOT NULL DEFAULT GETDATE(),
    Status         NVARCHAR(20)  NOT NULL DEFAULT 'Confirmed',
    CONSTRAINT FK_Enrolments_Participant FOREIGN KEY (ParticipantID) REFERENCES dbo.Users(UserID),
    CONSTRAINT FK_Enrolments_Event FOREIGN KEY (EventID) REFERENCES dbo.Events(EventID),
    CONSTRAINT FK_Enrolments_Category FOREIGN KEY (CategoryID) REFERENCES dbo.Categories(CategoryID),
    CONSTRAINT UQ_Enrolment UNIQUE (ParticipantID, EventID)  -- one enrolment per event
);

CREATE TABLE dbo.Result (
    ResultID     INT IDENTITY(1,1) PRIMARY KEY,
    EnrolmentID  INT           NOT NULL,
    FinishTime   TIME          NULL,          -- or INT seconds
    Position     INT           NULL,
    Notes        NVARCHAR(200) NULL,
    CapturedBy   INT           NOT NULL,
    CapturedDate DATETIME2     NOT NULL DEFAULT GETDATE(),
    CONSTRAINT UQ_Results_Enrolment UNIQUE (EnrolmentID),
    CONSTRAINT FK_Results_Enrolment FOREIGN KEY (EnrolmentID) REFERENCES dbo.Enrolments(EnrolmentID),
    CONSTRAINT FK_Results_CapturedBy FOREIGN KEY (CapturedBy) REFERENCES dbo.Users(UserID)
);

-- Seed lookup data
INSERT INTO dbo.EventTypes (Name, Description) VALUES
('Running', 'Road running events'),
('Walking', 'Race walking / fun walks'),
('Cycling', 'Road cycling events');

-- Seed minimal users (replace 'HASHED_PWD' with secure hashes in real deployments)
INSERT INTO dbo.Users (FirstName, LastName, Email, PasswordHash, Role, Phone) VALUES
('Ahlumile', 'Daka', 'ahlumile.organiser@raceday.co.za', 'HASHED_PWD', 'Organiser', '0821112233'),
('Slindokuhle', 'Daka', 'slindo.organiser@raceday.co.za', 'HASHED_PWD', 'Organiser', '0834445566'),
('Siphokuhle', 'Daku', 'sipho.runner@gmail.com', 'HASHED_PWD', 'Participant', '0719998877'),
('Nomsa', 'Mini', 'nomsa.walker@gmail.com', 'HASHED_PWD', 'Participant', '0723332211');

-- 3 Events + categories + enrolments (fill in the rest)
-- ... continue with realistic Cape Town / Joburg / Durban race data