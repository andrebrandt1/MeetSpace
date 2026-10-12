CREATE TABLE dbo.Rooms
(
    Id int IDENTITY(1, 1) NOT NULL CONSTRAINT PK_Rooms PRIMARY KEY,
    Name nvarchar(100) NOT NULL
);

CREATE TABLE dbo.Bookings
(
    Id int IDENTITY(1, 1) NOT NULL CONSTRAINT PK_Bookings PRIMARY KEY,
    RoomId int NOT NULL,
    BookedBy nvarchar(100) NOT NULL,
    Title nvarchar(100) NOT NULL,
    StartTime datetime2(7) NOT NULL,
    EndTime datetime2(7) NOT NULL,
    CONSTRAINT FK_Bookings_Rooms FOREIGN KEY (RoomId) REFERENCES dbo.Rooms (Id),
    CONSTRAINT CK_Bookings_TimeRange CHECK (EndTime > StartTime)
);

CREATE INDEX IX_Bookings_RoomId_StartTime
    ON dbo.Bookings (RoomId, StartTime) INCLUDE (EndTime);
