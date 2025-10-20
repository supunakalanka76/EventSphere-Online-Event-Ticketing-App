--  DATABASE: EventSphereDB
--  PURPOSE:  Event Management & Booking System

-- Create Database
CREATE DATABASE EventSphereDB;
GO

USE EventSphereDB;
GO

--  TABLE: Users
CREATE TABLE dbo.Users (
    UserID INT IDENTITY(1,1) PRIMARY KEY,
    FullName NVARCHAR(100) NOT NULL,
    Email NVARCHAR(100) UNIQUE NOT NULL,
    PasswordHash NVARCHAR(255) NOT NULL,
    Role NVARCHAR(50) CHECK (Role IN ('Admin', 'Organizer', 'Customer')) NOT NULL,
    Phone NVARCHAR(20),
    Gender NVARCHAR(10),
    DateOfBirth DATE,
    Address NVARCHAR(255),
    ProfileImage NVARCHAR(255),
    LoyaltyPoints INT DEFAULT 0,
    AccountStatus NVARCHAR(10) CHECK (AccountStatus IN ('Active', 'Inactive')) DEFAULT 'Active',
    CreatedAt DATETIME DEFAULT GETDATE(),
    UpdatedAt DATETIME DEFAULT GETDATE()
);
GO

--  TABLE: Venues
CREATE TABLE dbo.Venues (
    VenueID INT IDENTITY(1,1) PRIMARY KEY,
    VenueName NVARCHAR(100) NOT NULL,
    Address NVARCHAR(255),
    City NVARCHAR(100),
    Capacity INT,
    ContactNumber NVARCHAR(20),
    Email NVARCHAR(100),
    MapLink NVARCHAR(255),
    CreatedAt DATETIME DEFAULT GETDATE(),
    UpdatedAt DATETIME DEFAULT GETDATE()
);
GO

--  TABLE: EventCategories
CREATE TABLE dbo.EventCategories (
    CategoryID INT IDENTITY(1,1) PRIMARY KEY,
    CategoryName NVARCHAR(100) NOT NULL,
    Description NVARCHAR(255),
    IsActive BIT DEFAULT 1
);
GO

--  TABLE: Events
CREATE TABLE dbo.Events (
    EventID INT IDENTITY(1,1) PRIMARY KEY,
    OrganizerID INT NOT NULL,
    CategoryID INT NOT NULL,
    VenueID INT NOT NULL,
    Title NVARCHAR(150) NOT NULL,
    Description NVARCHAR(MAX),
    StartDate DATE,
    EndDate DATE,
    StartTime TIME,
    EndTime TIME,
    TicketPrice DECIMAL(10,2) NOT NULL,
    TotalTickets INT NOT NULL,
    AvailableTickets INT NOT NULL,
    EventImage NVARCHAR(255),
    Status NVARCHAR(20) CHECK (Status IN ('Approved', 'Pending')) DEFAULT 'Pending',
    CreatedAt DATETIME DEFAULT GETDATE(),
    UpdatedAt DATETIME DEFAULT GETDATE(),
    FOREIGN KEY (OrganizerID) REFERENCES dbo.Users(UserID),
    FOREIGN KEY (CategoryID) REFERENCES dbo.EventCategories(CategoryID),
    FOREIGN KEY (VenueID) REFERENCES dbo.Venues(VenueID)
);
GO

--  TABLE: Promotions
CREATE TABLE dbo.Promotions (
    PromotionID INT IDENTITY(1,1) PRIMARY KEY,
    Code NVARCHAR(50) UNIQUE NOT NULL,
    Description NVARCHAR(255),
    DiscountPercentage DECIMAL(5,2),
    StartDate DATE,
    EndDate DATE,
    UsageLimit INT,
    IsActive BIT DEFAULT 1
);
GO

--  TABLE: Bookings
CREATE TABLE dbo.Bookings (
    BookingID INT IDENTITY(1,1) PRIMARY KEY,
    UserID INT NOT NULL,
    EventID INT NOT NULL,
    PromotionID INT NULL,
    Quantity INT NOT NULL,
    TotalAmount DECIMAL(10,2),
    DiscountApplied DECIMAL(10,2),
    FinalAmount DECIMAL(10,2),
    PaymentStatus NVARCHAR(20) CHECK (PaymentStatus IN ('Completed', 'Pending')) DEFAULT 'Pending',
    PaymentMethod NVARCHAR(20) CHECK (PaymentMethod IN ('Cash', 'Card', 'Online')),
    BookingDate DATETIME DEFAULT GETDATE(),
    CheckInStatus BIT DEFAULT 0,
    Notes NVARCHAR(255),
    FOREIGN KEY (UserID) REFERENCES dbo.Users(UserID),
    FOREIGN KEY (EventID) REFERENCES dbo.Events(EventID),
    FOREIGN KEY (PromotionID) REFERENCES dbo.Promotions(PromotionID)
);
GO

--  TABLE: Payments
CREATE TABLE dbo.Payments (
    PaymentID INT IDENTITY(1,1) PRIMARY KEY,
    TransactionID NVARCHAR(50) UNIQUE NOT NULL,
    BookingID INT NOT NULL,
    Amount DECIMAL(10,2) NOT NULL,
    PaymentDate DATETIME DEFAULT GETDATE(),
    Status NVARCHAR(20) CHECK (Status IN ('Completed', 'Pending')) DEFAULT 'Pending',
    PaymentGateway NVARCHAR(20) CHECK (PaymentGateway IN ('Online', 'Cash', 'Card')),
    ReferenceNo NVARCHAR(100),
    Remarks NVARCHAR(255),
    InvoiceNumber NVARCHAR(50),
    FOREIGN KEY (BookingID) REFERENCES dbo.Bookings(BookingID)
);
GO

--  TABLE: Tickets
CREATE TABLE dbo.Tickets (
    TicketID INT IDENTITY(1,1) PRIMARY KEY,
    BookingID INT NOT NULL,
    TicketNumber NVARCHAR(50) UNIQUE NOT NULL,
    QRCodeImage NVARCHAR(255),
    IssueDate DATETIME DEFAULT GETDATE(),
    IsUsed BIT DEFAULT 0,
    UsedDate DATETIME NULL,
    FOREIGN KEY (BookingID) REFERENCES dbo.Bookings(BookingID)
);
GO

--  TABLE: LoyaltyTransactions
CREATE TABLE dbo.LoyaltyTransactions (
    TransactionID INT IDENTITY(1,1) PRIMARY KEY,
    UserID INT NOT NULL,
    BookingID INT NULL,
    Points INT NOT NULL,
    Type NVARCHAR(10) CHECK (Type IN ('Earn', 'Redeem')),
    Description NVARCHAR(255),
    TransactionDate DATETIME DEFAULT GETDATE(),
    FOREIGN KEY (UserID) REFERENCES dbo.Users(UserID),
    FOREIGN KEY (BookingID) REFERENCES dbo.Bookings(BookingID)
);
GO

--  DEFAULT SELECT PERMISSIONS (OPTIONAL)
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::dbo TO [public];
GO
