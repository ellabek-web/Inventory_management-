BEGIN TRANSACTION;
ALTER TABLE [Sales] ADD [CreatedByUserId] uniqueidentifier NULL;

ALTER TABLE [Sales] ADD [CustomerPhone] nvarchar(30) NULL;

ALTER TABLE [Sales] ADD [WarehouseId] uniqueidentifier NOT NULL;

DECLARE @var nvarchar(max);
SELECT @var = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SaleItems]') AND [c].[name] = N'Quantity');
IF @var IS NOT NULL EXEC(N'ALTER TABLE [SaleItems] DROP CONSTRAINT ' + @var + ';');
ALTER TABLE [SaleItems] ALTER COLUMN [Quantity] decimal(18,3) NOT NULL;

DECLARE @var1 nvarchar(max);
SELECT @var1 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PurchaseItems]') AND [c].[name] = N'Quantity');
IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [PurchaseItems] DROP CONSTRAINT ' + @var1 + ';');
ALTER TABLE [PurchaseItems] ALTER COLUMN [Quantity] decimal(18,3) NOT NULL;

CREATE TABLE [SalePayments] (
    [Id] uniqueidentifier NOT NULL,
    [SaleId] uniqueidentifier NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [PaymentMethod] nvarchar(40) NOT NULL,
    [Reference] nvarchar(100) NULL,
    [ReceivedAtUtc] datetimeoffset NOT NULL,
    [ReceivedByUserId] uniqueidentifier NULL,
    CONSTRAINT [PK_SalePayments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_SalePayments_Sales_SaleId] FOREIGN KEY ([SaleId]) REFERENCES [Sales] ([Id]) ON DELETE NO ACTION
);

CREATE INDEX [IX_Sales_TenantId_CreatedAtUtc] ON [Sales] ([TenantId], [CreatedAtUtc]);

CREATE INDEX [IX_Sales_WarehouseId] ON [Sales] ([WarehouseId]);

CREATE INDEX [IX_SalePayments_SaleId] ON [SalePayments] ([SaleId]);

ALTER TABLE [Sales] ADD CONSTRAINT [FK_Sales_Warehouses_WarehouseId] FOREIGN KEY ([WarehouseId]) REFERENCES [Warehouses] ([Id]) ON DELETE NO ACTION;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261031065227_AddSalesManagement', N'10.0.13');

COMMIT;
GO

