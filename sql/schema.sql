-- Run once in the Azure SQL Query editor
IF OBJECT_ID('Users') IS NULL CREATE TABLE Users(
  UserId INT IDENTITY PRIMARY KEY, Username NVARCHAR(50) NOT NULL UNIQUE,
  PasswordHash NVARCHAR(200) NOT NULL, Role NVARCHAR(20) NOT NULL CHECK (Role IN ('Farmer','Agronomist','Admin')));
IF OBJECT_ID('Devices') IS NULL CREATE TABLE Devices(
  DeviceId NVARCHAR(50) PRIMARY KEY, Name NVARCHAR(100) NOT NULL, Status NVARCHAR(30) NOT NULL DEFAULT 'Idle',
  LastCommand NVARCHAR(30) NULL, UpdatedAt DATETIME2 NULL);
IF NOT EXISTS (SELECT 1 FROM Devices) INSERT INTO Devices(DeviceId,Name,Status) VALUES
 ('PUMP-1','North field pump','Idle'),('PUMP-2','South field pump','Idle'),('PUMP-3','Orchard sprinklers','Idle');
