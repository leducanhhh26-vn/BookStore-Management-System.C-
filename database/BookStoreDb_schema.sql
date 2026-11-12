IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111032441_InitialCreate'
)
BEGIN
    CREATE TABLE [Authors] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [Bio] nvarchar(1000) NULL,
        CONSTRAINT [PK_Authors] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111032441_InitialCreate'
)
BEGIN
    CREATE TABLE [Categories] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [Description] nvarchar(500) NULL,
        [IsHidden] bit NOT NULL,
        CONSTRAINT [PK_Categories] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111032441_InitialCreate'
)
BEGIN
    CREATE TABLE [Coupons] (
        [Id] int NOT NULL IDENTITY,
        [Code] varchar(30) NOT NULL,
        [DiscountType] nvarchar(20) NOT NULL,
        [Value] decimal(18,2) NOT NULL,
        [MaxDiscount] decimal(18,2) NULL,
        [MinOrder] decimal(18,2) NOT NULL,
        [StartAt] datetime2 NOT NULL,
        [EndAt] datetime2 NOT NULL,
        [UsageLimit] int NOT NULL,
        [UsedCount] int NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Coupons] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Coupons_Dates] CHECK (EndAt > StartAt),
        CONSTRAINT [CK_Coupons_MinOrder] CHECK (MinOrder >= 0),
        CONSTRAINT [CK_Coupons_Usage] CHECK (UsageLimit > 0 AND UsedCount >= 0),
        CONSTRAINT [CK_Coupons_Value] CHECK (Value > 0)
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111032441_InitialCreate'
)
BEGIN
    CREATE TABLE [Publishers] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(150) NOT NULL,
        [Address] nvarchar(200) NULL,
        [Phone] varchar(15) NULL,
        CONSTRAINT [PK_Publishers] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111032441_InitialCreate'
)
BEGIN
    CREATE TABLE [Suppliers] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(150) NOT NULL,
        [Phone] varchar(15) NULL,
        [Email] nvarchar(100) NULL,
        [Address] nvarchar(200) NULL,
        CONSTRAINT [PK_Suppliers] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111032441_InitialCreate'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] int NOT NULL IDENTITY,
        [Username] nvarchar(50) NOT NULL,
        [PasswordHash] varchar(100) NOT NULL,
        [FullName] nvarchar(100) NOT NULL,
        [Email] nvarchar(100) NOT NULL,
        [Phone] varchar(15) NOT NULL,
        [Role] nvarchar(20) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111032441_InitialCreate'
)
BEGIN
    CREATE TABLE [Books] (
        [Id] int NOT NULL IDENTITY,
        [CategoryId] int NOT NULL,
        [PublisherId] int NOT NULL,
        [Isbn] varchar(20) NOT NULL,
        [Title] nvarchar(250) NOT NULL,
        [PublishYear] int NOT NULL,
        [Pages] int NOT NULL,
        [CoverType] nvarchar(50) NOT NULL,
        [Description] nvarchar(4000) NULL,
        [Price] decimal(18,2) NOT NULL,
        [Stock] int NOT NULL,
        [LowStockThreshold] int NOT NULL,
        [IsFeatured] bit NOT NULL,
        [IsHidden] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_Books] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Books_LowStockThreshold] CHECK (LowStockThreshold >= 0),
        CONSTRAINT [CK_Books_Pages] CHECK (Pages > 0),
        CONSTRAINT [CK_Books_Price] CHECK (Price > 0),
        CONSTRAINT [CK_Books_Stock] CHECK (Stock >= 0),
        CONSTRAINT [FK_Books_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Books_Publishers_PublisherId] FOREIGN KEY ([PublisherId]) REFERENCES [Publishers] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111032441_InitialCreate'
)
BEGIN
    CREATE TABLE [CustomerAddresses] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [ReceiverName] nvarchar(100) NOT NULL,
        [Phone] varchar(15) NOT NULL,
        [AddressLine] nvarchar(200) NOT NULL,
        [Ward] nvarchar(100) NOT NULL,
        [District] nvarchar(100) NOT NULL,
        [Province] nvarchar(100) NOT NULL,
        [IsDefault] bit NOT NULL,
        CONSTRAINT [PK_CustomerAddresses] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CustomerAddresses_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111032441_InitialCreate'
)
BEGIN
    CREATE TABLE [ImportReceipts] (
        [Id] int NOT NULL IDENTITY,
        [SupplierId] int NOT NULL,
        [StaffId] int NOT NULL,
        [ImportDate] datetime2 NOT NULL,
        [Note] nvarchar(500) NULL,
        CONSTRAINT [PK_ImportReceipts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ImportReceipts_Suppliers_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [Suppliers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ImportReceipts_Users_StaffId] FOREIGN KEY ([StaffId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111032441_InitialCreate'
)
BEGIN
    CREATE TABLE [Orders] (
        [Id] int NOT NULL IDENTITY,
        [CustomerId] int NOT NULL,
        [HandledById] int NULL,
        [CouponId] int NULL,
        [OrderDate] datetime2 NOT NULL,
        [CompletedAt] datetime2 NULL,
        [ReceiverName] nvarchar(100) NOT NULL,
        [ReceiverPhone] varchar(15) NOT NULL,
        [ShippingAddress] nvarchar(300) NOT NULL,
        [Note] nvarchar(500) NULL,
        [PaymentMethod] nvarchar(20) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [Subtotal] decimal(18,2) NOT NULL,
        [DiscountAmount] decimal(18,2) NOT NULL,
        [ShippingFee] decimal(18,2) NOT NULL,
        [CancelReason] nvarchar(500) NULL,
        [RowVersion] rowversion NULL,
        CONSTRAINT [PK_Orders] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Orders_Money] CHECK (Subtotal >= 0 AND DiscountAmount >= 0 AND ShippingFee >= 0 AND DiscountAmount <= Subtotal),
        CONSTRAINT [FK_Orders_Coupons_CouponId] FOREIGN KEY ([CouponId]) REFERENCES [Coupons] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Orders_Users_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Orders_Users_HandledById] FOREIGN KEY ([HandledById]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111032441_InitialCreate'
)
BEGIN
    CREATE TABLE [BookAuthors] (
        [BookId] int NOT NULL,
        [AuthorId] int NOT NULL,
        CONSTRAINT [PK_BookAuthors] PRIMARY KEY ([BookId], [AuthorId]),
        CONSTRAINT [FK_BookAuthors_Authors_AuthorId] FOREIGN KEY ([AuthorId]) REFERENCES [Authors] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BookAuthors_Books_BookId] FOREIGN KEY ([BookId]) REFERENCES [Books] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111032441_InitialCreate'
)
BEGIN
    CREATE TABLE [BookImages] (
        [BookId] int NOT NULL,
        [Data] varbinary(max) NOT NULL,
        [ContentType] varchar(50) NOT NULL,
        CONSTRAINT [PK_BookImages] PRIMARY KEY ([BookId]),
        CONSTRAINT [FK_BookImages_Books_BookId] FOREIGN KEY ([BookId]) REFERENCES [Books] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111032441_InitialCreate'
)
BEGIN
    CREATE TABLE [CartItems] (
        [UserId] int NOT NULL,
        [BookId] int NOT NULL,
        [Quantity] int NOT NULL,
        [AddedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_CartItems] PRIMARY KEY ([UserId], [BookId]),
        CONSTRAINT [CK_CartItems_Quantity] CHECK (Quantity > 0),
        CONSTRAINT [FK_CartItems_Books_BookId] FOREIGN KEY ([BookId]) REFERENCES [Books] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_CartItems_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111032441_InitialCreate'
)
BEGIN
    CREATE TABLE [Reviews] (
        [UserId] int NOT NULL,
        [BookId] int NOT NULL,
        [Rating] int NOT NULL,
        [Comment] nvarchar(1000) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Reviews] PRIMARY KEY ([UserId], [BookId]),
        CONSTRAINT [CK_Reviews_Rating] CHECK (Rating BETWEEN 1 AND 5),
        CONSTRAINT [FK_Reviews_Books_BookId] FOREIGN KEY ([BookId]) REFERENCES [Books] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_Reviews_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111032441_InitialCreate'
)
BEGIN
    CREATE TABLE [StockTransactions] (
        [Id] int NOT NULL IDENTITY,
        [BookId] int NOT NULL,
        [StaffId] int NULL,
        [Type] nvarchar(20) NOT NULL,
        [QuantityChange] int NOT NULL,
        [StockAfter] int NOT NULL,
        [RefId] int NULL,
        [Reason] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_StockTransactions] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_StockTransactions_QuantityChange] CHECK (QuantityChange <> 0),
        CONSTRAINT [CK_StockTransactions_StockAfter] CHECK (StockAfter >= 0),
        CONSTRAINT [FK_StockTransactions_Books_BookId] FOREIGN KEY ([BookId]) REFERENCES [Books] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StockTransactions_Users_StaffId] FOREIGN KEY ([StaffId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111032441_InitialCreate'
)
BEGIN
    CREATE TABLE [ImportReceiptDetails] (
        [ReceiptId] int NOT NULL,
        [BookId] int NOT NULL,
        [Quantity] int NOT NULL,
        [UnitCost] decimal(18,2) NOT NULL,
        CONSTRAINT [PK_ImportReceiptDetails] PRIMARY KEY ([ReceiptId], [BookId]),
        CONSTRAINT [CK_ImportReceiptDetails_Quantity] CHECK (Quantity > 0),
        CONSTRAINT [CK_ImportReceiptDetails_UnitCost] CHECK (UnitCost >= 0),
        CONSTRAINT [FK_ImportReceiptDetails_Books_BookId] FOREIGN KEY ([BookId]) REFERENCES [Books] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ImportReceiptDetails_ImportReceipts_ReceiptId] FOREIGN KEY ([ReceiptId]) REFERENCES [ImportReceipts] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111032441_InitialCreate'
)
BEGIN
    CREATE TABLE [OrderDetails] (
        [OrderId] int NOT NULL,
        [BookId] int NOT NULL,
        [Quantity] int NOT NULL,
        [UnitPrice] decimal(18,2) NOT NULL,
        CONSTRAINT [PK_OrderDetails] PRIMARY KEY ([OrderId], [BookId]),
        CONSTRAINT [CK_OrderDetails_Quantity] CHECK (Quantity > 0),
        CONSTRAINT [CK_OrderDetails_UnitPrice] CHECK (UnitPrice >= 0),
        CONSTRAINT [FK_OrderDetails_Books_BookId] FOREIGN KEY ([BookId]) REFERENCES [Books] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_OrderDetails_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111032441_InitialCreate'
)
BEGIN
    CREATE TABLE [Payments] (
        [Id] int NOT NULL IDENTITY,
        [OrderId] int NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Status] nvarchar(20) NOT NULL,
        [PaidAt] datetime2 NULL,
        [ReferenceCode] varchar(100) NULL,
        CONSTRAINT [PK_Payments] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Payments_Amount] CHECK (Amount > 0),
        CONSTRAINT [FK_Payments_Orders_OrderId] FOREIGN KEY ([OrderId]) REFERENCES [Orders] ([Id]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111032441_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Authors_Name] ON [Authors] ([Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111032441_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_BookAuthors_AuthorId] ON [BookAuthors] ([AuthorId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111032441_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Books_CategoryId_IsHidden] ON [Books] ([CategoryId], [IsHidden]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111032441_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Books_Isbn] ON [Books] ([Isbn]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111032441_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Books_PublisherId] ON [Books] ([PublisherId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111032441_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Books_Title] ON [Books] ([Title]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111032441_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CartItems_BookId] ON [CartItems] ([BookId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111032441_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Categories_Name] ON [Categories] ([Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111032441_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Coupons_Code] ON [Coupons] ([Code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111032441_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_CustomerAddresses_UserId] ON [CustomerAddresses] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111032441_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ImportReceiptDetails_BookId] ON [ImportReceiptDetails] ([BookId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111032441_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ImportReceipts_ImportDate] ON [ImportReceipts] ([ImportDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111032441_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ImportReceipts_StaffId] ON [ImportReceipts] ([StaffId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111032441_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ImportReceipts_SupplierId] ON [ImportReceipts] ([SupplierId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111032441_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OrderDetails_BookId] ON [OrderDetails] ([BookId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111032441_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Orders_CouponId] ON [Orders] ([CouponId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111032441_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Orders_CustomerId_OrderDate] ON [Orders] ([CustomerId], [OrderDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111032441_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Orders_HandledById] ON [Orders] ([HandledById]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111032441_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Orders_Status_CompletedAt] ON [Orders] ([Status], [CompletedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111032441_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Payments_OrderId] ON [Payments] ([OrderId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111032441_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Payments_ReferenceCode] ON [Payments] ([ReferenceCode]) WHERE ReferenceCode IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111032441_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Publishers_Name] ON [Publishers] ([Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111032441_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Reviews_BookId] ON [Reviews] ([BookId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111032441_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_StockTransactions_BookId_CreatedAt] ON [StockTransactions] ([BookId], [CreatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111032441_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_StockTransactions_StaffId] ON [StockTransactions] ([StaffId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111032441_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111032441_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Phone] ON [Users] ([Phone]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111032441_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Username] ON [Users] ([Username]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261111032441_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261111032441_InitialCreate', N'8.0.11');
END;
GO

COMMIT;
GO

