USE master;
GO

IF DB_ID(N'MeetSpace') IS NULL
    EXEC(N'CREATE DATABASE MeetSpace');
GO

USE MeetSpace;
GO

-- Tabellen nedan används också av testerna i deras tillfälliga databas.

IF OBJECT_ID(N'dbo.Employees', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Employees
    (
        Id INT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_Employees PRIMARY KEY,
        Name NVARCHAR(100) NOT NULL,
        Email NVARCHAR(254) NOT NULL CONSTRAINT UQ_Employees_Email UNIQUE
    );
END;
GO

IF OBJECT_ID(N'dbo.Rooms', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Rooms
    (
        Id INT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_Rooms PRIMARY KEY,
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
        Id INT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_Bookings PRIMARY KEY,
        RoomId INT NOT NULL,
        BookedBy NVARCHAR(100) NOT NULL,
        Title NVARCHAR(100) NOT NULL,
        StartTime DATETIME2(0) NOT NULL,
        EndTime DATETIME2(0) NOT NULL,
        CONSTRAINT FK_Bookings_Rooms FOREIGN KEY (RoomId) REFERENCES dbo.Rooms(Id)
    );
END;
GO
