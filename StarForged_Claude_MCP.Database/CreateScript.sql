create database Embedders;
go
use Embedders;
go

create table Documents
(
    Id int identity(1,1) primary key,
    Category nvarchar(200) not null,
    Filename nvarchar(500) not null,
    Content nvarchar(max) not null,
    Summary nvarchar(512) null,
    constraint UQ_Documents_Category_Filename unique (Category, Filename)
);

create table Embeddings
(
    Id int identity(1,1) primary key,
    DocumentId int not null,
    Text nvarchar(max) not null,
    Vector varbinary(8000) not null,
    TokenCount int not null,
    constraint FK_Embeddings_Documents foreign key (DocumentId) references Documents (Id) on delete cascade
);

create index IX_Embeddings_DocumentId on Embeddings (DocumentId);

create table Campaigns
(
    Id int identity(1,1) primary key,
    Name nvarchar(30) not null,
    constraint UQ_Campaigns_Name unique (Name)
);

create table Meters
(
    CampaignId int not null,
    Name nvarchar(100) not null,
    Value int not null,
    MinValue int not null,
    MaxValue int null,
    constraint PK_Meters primary key (CampaignId, Name),
    constraint FK_Meters_Campaigns foreign key (CampaignId) references Campaigns (Id) on delete cascade
);

create table Tracks
(
    CampaignId int not null,
    Kind nvarchar(20) not null,
    Name nvarchar(100) not null,
    Rank nvarchar(20) not null,
    Description nvarchar(200) not null,
    Ticks int not null,
    constraint PK_Tracks primary key (CampaignId, Kind, Name),
    constraint FK_Tracks_Campaigns foreign key (CampaignId) references Campaigns (Id) on delete cascade
);

create table Impacts
(
    CampaignId int not null,
    ImpactedEntity nvarchar(100) not null,
    Name nvarchar(100) not null,
    constraint PK_Impacts primary key (CampaignId, ImpactedEntity, Name),
    constraint FK_Impacts_Campaigns foreign key (CampaignId) references Campaigns (Id) on delete cascade
);

create table Checkpoints
(
    CampaignId int not null,
    Name nvarchar(100) not null,
    Snapshot nvarchar(max) not null,
    constraint PK_Checkpoints primary key (CampaignId, Name),
    constraint FK_Checkpoints_Campaigns foreign key (CampaignId) references Campaigns (Id) on delete cascade
);