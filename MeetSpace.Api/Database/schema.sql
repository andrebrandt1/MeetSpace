CREATE TABLE dbo.Rooms
(
    Id int IDENTITY(1, 1) NOT NULL CONSTRAINT PK_Rooms PRIMARY KEY,
    Name nvarchar(50) NOT NULL,
    Description nvarchar(1000) NULL,
    Capacity int NOT NULL CONSTRAINT DF_Rooms_Capacity DEFAULT 1,
    Floor int NOT NULL CONSTRAINT DF_Rooms_Floor DEFAULT 0
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
