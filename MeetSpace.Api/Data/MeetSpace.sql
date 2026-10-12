USE master;
GO

IF DB_ID(N'MeetSpace') IS NULL
    EXEC(N'CREATE DATABASE MeetSpace');
GO

USE MeetSpace;
GO

IF OBJECT_ID(N'dbo.Rooms', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Rooms
    (
        Id INT IDENTITY(1, 1) NOT NULL
            CONSTRAINT PK_Rooms PRIMARY KEY,
        Name NVARCHAR(50) NOT NULL,
        Description NVARCHAR(1000) NULL,
        Capacity INT NOT NULL,
        Floor INT NOT NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.Bookings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Bookings
    (
        Id INT IDENTITY(1, 1) NOT NULL
            CONSTRAINT PK_Bookings PRIMARY KEY,
        RoomId INT NOT NULL,
        BookedBy NVARCHAR(100) NOT NULL,
        Title NVARCHAR(100) NOT NULL,
        StartTime DATETIME2(0) NOT NULL,
        EndTime DATETIME2(0) NOT NULL,

        CONSTRAINT FK_Bookings_Rooms
            FOREIGN KEY (RoomId) REFERENCES dbo.Rooms(Id)
    );
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.Bookings')
      AND name = N'CK_Bookings_TimeRange'
)
BEGIN
    ALTER TABLE dbo.Bookings
    ADD CONSTRAINT CK_Bookings_TimeRange
        CHECK (EndTime > StartTime);
END;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Bookings')
      AND name = N'IX_Bookings_RoomId_StartTime'
)
BEGIN
    CREATE INDEX IX_Bookings_RoomId_StartTime
        ON dbo.Bookings (RoomId, StartTime)
        INCLUDE (EndTime);
END;
GO