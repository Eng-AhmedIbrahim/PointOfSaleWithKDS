namespace POS.Services.DataSyncServices;

public class DataSyncService : IDataSyncService
{
    private readonly AppDbContext _context;
    private readonly IEncryptionService _encryption;

    public DataSyncService(AppDbContext context, IEncryptionService encryption)
    {
        _context = context;
        _encryption = encryption;
    }

    public async Task<HqSettingDto> GetHqSettingsAsync()
    {
        var setting = await _context.HqSettings.OrderBy(x => x.Id).FirstOrDefaultAsync();
        if (setting == null) return new HqSettingDto();

        return new HqSettingDto
        {
            Id = setting.Id,
            ServerName = setting.ServerName,
            DatabaseName = _encryption.Decrypt(setting.DatabaseName),
            UserName = _encryption.Decrypt(setting.UserName),
            Password = _encryption.Decrypt(setting.Password),
            UseIntegratedSecurity = setting.UseIntegratedSecurity,
            IsEnabled = setting.IsEnabled,
            LastSyncDate = setting.LastSyncDate,
            ConnectTimeout = setting.ConnectTimeout,
            TrustServerCertificate = setting.TrustServerCertificate,
            Encrypt = setting.Encrypt
        };
    }

    public async Task<BaseResponse> UpdateHqSettingsAsync(HqSettingDto settings)
    {
        var existing = await _context.HqSettings.OrderBy(x => x.Id).FirstOrDefaultAsync();
        if (existing == null)
        {
            existing = new HqSetting
            {
                ServerName = settings.ServerName,
                DatabaseName = _encryption.Encrypt(settings.DatabaseName),
                UserName = _encryption.Encrypt(settings.UserName),
                Password = _encryption.Encrypt(settings.Password),
                UseIntegratedSecurity = settings.UseIntegratedSecurity,
                IsEnabled = settings.IsEnabled,
                ConnectTimeout = settings.ConnectTimeout,
                TrustServerCertificate = settings.TrustServerCertificate,
                Encrypt = settings.Encrypt
            };
            _context.HqSettings.Add(existing);
        }
        else
        {
            existing.ServerName = settings.ServerName;
            existing.DatabaseName = _encryption.Encrypt(settings.DatabaseName);
            existing.UserName = _encryption.Encrypt(settings.UserName);
            existing.Password = _encryption.Encrypt(settings.Password);
            existing.UseIntegratedSecurity = settings.UseIntegratedSecurity;
            existing.IsEnabled = settings.IsEnabled;
            existing.ConnectTimeout = settings.ConnectTimeout;
            existing.TrustServerCertificate = settings.TrustServerCertificate;
            existing.Encrypt = settings.Encrypt;
        }

        await _context.SaveChangesAsync();
        return new BaseResponse { Success = true, Message = "Settings updated successfully" };
    }

    public async Task<BaseResponse> TestHqConnectionAsync(HqSettingDto settings)
    {
        string connString = BuildConnectionString(settings);

        try
        {
            using (var conn = new SqlConnection(connString))
            {
                await conn.OpenAsync();
                return new BaseResponse { Success = true, Message = "Connection successful!" };
            }
        }
        catch (Exception ex)
        {
            return new BaseResponse { Success = false, Message = $"Connection failed: {ex.Message}" };
        }
    }

    public async Task<BaseResponse> SyncDataFromHqAsync(SyncRequestDto request)
    {
        var setting = await _context.HqSettings.OrderBy(x => x.Id).FirstOrDefaultAsync();
        if (setting == null)
            return new BaseResponse { Success = false, Message = "HQ Settings not configured. Please save the connection settings first." };

        var appDate = await _context.AppDate.OrderByDescending(x => x.Id).FirstOrDefaultAsync();
        int localBranchId = appDate?.BranchId ?? 0;

        var decryptedSetting = new HqSetting
        {
            ServerName = setting.ServerName,
            DatabaseName = _encryption.Decrypt(setting.DatabaseName),
            UserName = _encryption.Decrypt(setting.UserName),
            Password = _encryption.Decrypt(setting.Password),
            UseIntegratedSecurity = setting.UseIntegratedSecurity,
            ConnectTimeout = setting.ConnectTimeout,
            TrustServerCertificate = setting.TrustServerCertificate,
            Encrypt = setting.Encrypt
        };

        var hqConnString = decryptedSetting.GetConnectionString();
        var localConnString = _context.Database.GetConnectionString() ?? throw new InvalidOperationException("Local connection string not found.");

        using (var localConn = new SqlConnection(localConnString))
        {
            await localConn.OpenAsync();
            using (var transaction = localConn.BeginTransaction())
            {
                try
                {
                    var disableAll = new SqlCommand("EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL'", localConn, transaction);
                    await disableAll.ExecuteNonQueryAsync();

                    var tablesToSync = new List<string>();

                    if (request.SyncMasterData)
                    {
                        tablesToSync.AddRange(new[] { "Companies", "Branches" });
                    }

                    // Perform Sync for Master Data
                    foreach (var table in tablesToSync)
                    {
                        await SyncTable(hqConnString, table, localConn, transaction, localBranchId);
                    }

                    // Incremental Sales Items Sync: Preserves local kitchen printers, local items, and matches IDs exactly
                    if (request.SyncSalesItems)
                    {
                        await SyncSalesItemsIncremental(hqConnString, localConn, transaction, localBranchId);
                    }

                    // 2. Re-enable all constraints
                    var enableAll = new SqlCommand("EXEC sp_MSforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL'", localConn, transaction);
                    await enableAll.ExecuteNonQueryAsync();

                    transaction.Commit();

                    setting.LastSyncDate = DateTime.Now;
                    await _context.SaveChangesAsync();

                    return new BaseResponse { Success = true, Message = "Synchronization completed successfully. Data is now identical to HQ while keeping local settings." };
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return new BaseResponse { Success = false, Message = $"Synchronization failed: {ex.Message}" };
                }
            }
        }
    }

    public async Task<List<string>> GetDatabasesAsync(HqSettingDto settings)
    {
        var databases = new List<string>();
        var masterSettings = new HqSettingDto
        {
            ServerName = settings.ServerName,
            DatabaseName = "master",
            UserName = settings.UserName,
            Password = settings.Password,
            UseIntegratedSecurity = settings.UseIntegratedSecurity,
            ConnectTimeout = 5,
            TrustServerCertificate = settings.TrustServerCertificate,
            Encrypt = settings.Encrypt
        };

        string connString = BuildConnectionString(masterSettings);

        try
        {
            using (var conn = new SqlConnection(connString))
            {
                await conn.OpenAsync();
                var cmd = new SqlCommand("SELECT name FROM sys.databases WHERE database_id > 4", conn);
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        databases.Add(reader.GetString(0));
                    }
                }
            }
        }
        catch { }
        return databases;
    }

    private async Task SyncTable(string hqConnString, string tableName, SqlConnection localConn, SqlTransaction transaction, int localBranchId)
    {
        using (var hqConn = new SqlConnection(hqConnString))
        {
            await hqConn.OpenAsync();
            var cmd = new SqlCommand($"SELECT * FROM {tableName}", hqConn);
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                var dt = new DataTable();
                dt.Load(reader);

                var branchIdColumn = dt.Columns.Cast<DataColumn>()
                    .FirstOrDefault(c => string.Equals(c.ColumnName, "BranchId", StringComparison.OrdinalIgnoreCase) || 
                                         string.Equals(c.ColumnName, "BranchID", StringComparison.OrdinalIgnoreCase));

                if (branchIdColumn != null && localBranchId > 0)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        row[branchIdColumn] = localBranchId;
                    }
                }

                var disableCmd = new SqlCommand($"ALTER TABLE {tableName} NOCHECK CONSTRAINT ALL", localConn, transaction);
                await disableCmd.ExecuteNonQueryAsync();

                var deleteCmd = new SqlCommand($"DELETE FROM {tableName}", localConn, transaction);
                await deleteCmd.ExecuteNonQueryAsync();

                if (dt.Rows.Count > 0)
                {
                    using (var bulkCopy = new SqlBulkCopy(localConn, SqlBulkCopyOptions.KeepIdentity, transaction))
                    {
                        bulkCopy.DestinationTableName = tableName;
                        foreach (DataColumn column in dt.Columns)
                        {
                            bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
                        }
                        await bulkCopy.WriteToServerAsync(dt);
                    }
                }

                var enableCmd = new SqlCommand($"ALTER TABLE {tableName} CHECK CONSTRAINT ALL", localConn, transaction);
                await enableCmd.ExecuteNonQueryAsync();
            }
        }
    }

    private string BuildConnectionString(HqSettingDto settings)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = settings.ServerName,
            InitialCatalog = settings.DatabaseName,
            IntegratedSecurity = settings.UseIntegratedSecurity,
            ConnectTimeout = settings.ConnectTimeout,
            TrustServerCertificate = settings.TrustServerCertificate,
            Encrypt = settings.Encrypt
        };

        if (!settings.UseIntegratedSecurity)
        {
            builder.UserID = settings.UserName;
            builder.Password = settings.Password;
        }

        return builder.ConnectionString;
    }

    private async Task SyncSalesItemsIncremental(string hqConnString, SqlConnection localConn, SqlTransaction transaction, int localBranchId)
    {
        using (var hqConn = new SqlConnection(hqConnString))
        {
            await hqConn.OpenAsync();

            // 1. KitchenTypes (upsert by Id, preserve existing)
            await UpsertTableById(hqConn, localConn, transaction, "KitchenTypes", new[] { "KitchenName" });

            // 2. Classifications & Attributes
            await UpsertTableById(hqConn, localConn, transaction, "ItemsClassifications", new[] { "Name", "ArabicName", "Description" });
            await UpsertTableById(hqConn, localConn, transaction, "AttributeGroups", new[] { "Name", "ArabicName" });
            await UpsertTableById(hqConn, localConn, transaction, "Attributes", new[] { "Name", "ArabicName", "SelectionType", "AttributeGroupId" });
            await UpsertTableById(hqConn, localConn, transaction, "AttributeItems", new[] { "Name", "ArabicName", "Price", "AttributeId" });

            // 3. Categories (upsert by Id)
            await UpsertTableById(hqConn, localConn, transaction, "Categories", new[] { "ArabicName", "EnglishName", "Description", "ImagePath", "MainCategoryId", "BackColor", "TextColor", "TextSize", "Active" });

            // 4. MenuSalesItems: Match by Id, preserve local printers (KitchenTypeId, PrintInBackupReceipt)
            var checkCmd = new SqlCommand("SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'MenuSalesItems'", hqConn);
            if (Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) == 0) return;

            var cmd = new SqlCommand(@"
SELECT Id, Barcode, ArabicName, EnglishName, NormalizedEnglishName, Price, SecondPrice, ThirdPrice,
       FourthPrice, FifthPrice, Tax, Description, ImagePath, MainCategoryId, CategoryId,
       BackColor, TextColor, TextSize, Invisible, HasAttribute, AttributeId, ByWeight,
       IsInventory, ItemTypeId, KitchenTypeId, PrintInBackupReceipt, CreationDate
FROM MenuSalesItems", hqConn);

            using (var reader = await cmd.ExecuteReaderAsync())
            {
                var dt = new DataTable();
                dt.Load(reader);

                var upsertSql = @"
IF EXISTS (SELECT 1 FROM MenuSalesItems WHERE Id = @Id)
BEGIN
    UPDATE MenuSalesItems
    SET ArabicName = @ArabicName,
        EnglishName = @EnglishName,
        NormalizedEnglishName = @NormalizedEnglishName,
        Barcode = CASE WHEN (@Barcode IS NOT NULL AND @Barcode <> '') THEN @Barcode ELSE Barcode END,
        Price = @Price,
        SecondPrice = @SecondPrice,
        ThirdPrice = @ThirdPrice,
        FourthPrice = @FourthPrice,
        FifthPrice = @FifthPrice,
        Tax = @Tax,
        Description = @Description,
        ImagePath = @ImagePath,
        MainCategoryId = @MainCategoryId,
        CategoryId = @CategoryId,
        BackColor = @BackColor,
        TextColor = @TextColor,
        TextSize = @TextSize,
        Invisible = @Invisible,
        HasAttribute = @HasAttribute,
        AttributeId = @AttributeId,
        ByWeight = @ByWeight,
        IsInventory = @IsInventory,
        ItemTypeId = @ItemTypeId,
        UpdatedDate = GETUTCDATE()
        -- Note: KitchenTypeId and PrintInBackupReceipt are NOT overwritten to protect local branch printer setup!
    WHERE Id = @Id;
END
ELSE
BEGIN
    SET IDENTITY_INSERT MenuSalesItems ON;
    INSERT INTO MenuSalesItems (
        Id, Barcode, ArabicName, EnglishName, NormalizedEnglishName, Price, SecondPrice, ThirdPrice,
        FourthPrice, FifthPrice, Tax, Description, ImagePath, MainCategoryId, CategoryId,
        BackColor, TextColor, TextSize, Invisible, HasAttribute, AttributeId, ByWeight,
        IsInventory, ItemTypeId, KitchenTypeId, PrintInBackupReceipt, BranchId, CreationDate
    ) VALUES (
        @Id, @Barcode, @ArabicName, @EnglishName, @NormalizedEnglishName, @Price, @SecondPrice, @ThirdPrice,
        @FourthPrice, @FifthPrice, @Tax, @Description, @ImagePath, @MainCategoryId, @CategoryId,
        @BackColor, @TextColor, @TextSize, @Invisible, @HasAttribute, @AttributeId, @ByWeight,
        @IsInventory, @ItemTypeId, @KitchenTypeId, @PrintInBackupReceipt, @BranchId, @CreationDate
    );
    SET IDENTITY_INSERT MenuSalesItems OFF;
END";

                foreach (DataRow row in dt.Rows)
                {
                    using (var upsertCmd = new SqlCommand(upsertSql, localConn, transaction))
                    {
                        upsertCmd.Parameters.AddWithValue("@Id", Convert.ToInt32(row["Id"]));
                        upsertCmd.Parameters.AddWithValue("@Barcode", row["Barcode"] != DBNull.Value ? row["Barcode"] : DBNull.Value);
                        upsertCmd.Parameters.AddWithValue("@ArabicName", row["ArabicName"]);
                        upsertCmd.Parameters.AddWithValue("@EnglishName", row["EnglishName"]);
                        upsertCmd.Parameters.AddWithValue("@NormalizedEnglishName", row["NormalizedEnglishName"] != DBNull.Value ? row["NormalizedEnglishName"] : (object)row["EnglishName"].ToString().ToUpperInvariant());
                        upsertCmd.Parameters.AddWithValue("@Price", row["Price"] != DBNull.Value ? row["Price"] : DBNull.Value);
                        upsertCmd.Parameters.AddWithValue("@SecondPrice", row["SecondPrice"] != DBNull.Value ? row["SecondPrice"] : DBNull.Value);
                        upsertCmd.Parameters.AddWithValue("@ThirdPrice", row["ThirdPrice"] != DBNull.Value ? row["ThirdPrice"] : DBNull.Value);
                        upsertCmd.Parameters.AddWithValue("@FourthPrice", row["FourthPrice"] != DBNull.Value ? row["FourthPrice"] : DBNull.Value);
                        upsertCmd.Parameters.AddWithValue("@FifthPrice", row["FifthPrice"] != DBNull.Value ? row["FifthPrice"] : DBNull.Value);
                        upsertCmd.Parameters.AddWithValue("@Tax", row["Tax"] != DBNull.Value ? row["Tax"] : DBNull.Value);
                        upsertCmd.Parameters.AddWithValue("@Description", row["Description"] != DBNull.Value ? row["Description"] : DBNull.Value);
                        upsertCmd.Parameters.AddWithValue("@ImagePath", row["ImagePath"] != DBNull.Value ? row["ImagePath"] : DBNull.Value);
                        upsertCmd.Parameters.AddWithValue("@MainCategoryId", row["MainCategoryId"] != DBNull.Value ? row["MainCategoryId"] : DBNull.Value);
                        upsertCmd.Parameters.AddWithValue("@CategoryId", row["CategoryId"] != DBNull.Value ? row["CategoryId"] : DBNull.Value);
                        upsertCmd.Parameters.AddWithValue("@BackColor", row["BackColor"] != DBNull.Value ? row["BackColor"] : DBNull.Value);
                        upsertCmd.Parameters.AddWithValue("@TextColor", row["TextColor"] != DBNull.Value ? row["TextColor"] : DBNull.Value);
                        upsertCmd.Parameters.AddWithValue("@TextSize", row["TextSize"] != DBNull.Value ? row["TextSize"] : DBNull.Value);
                        upsertCmd.Parameters.AddWithValue("@Invisible", Convert.ToBoolean(row["Invisible"]));
                        upsertCmd.Parameters.AddWithValue("@HasAttribute", Convert.ToBoolean(row["HasAttribute"]));
                        upsertCmd.Parameters.AddWithValue("@AttributeId", row["AttributeId"] != DBNull.Value ? row["AttributeId"] : DBNull.Value);
                        upsertCmd.Parameters.AddWithValue("@ByWeight", Convert.ToBoolean(row["ByWeight"]));
                        upsertCmd.Parameters.AddWithValue("@IsInventory", Convert.ToBoolean(row["IsInventory"]));
                        upsertCmd.Parameters.AddWithValue("@ItemTypeId", row["ItemTypeId"] != DBNull.Value ? row["ItemTypeId"] : 1);
                        upsertCmd.Parameters.AddWithValue("@KitchenTypeId", row["KitchenTypeId"] != DBNull.Value ? row["KitchenTypeId"] : DBNull.Value);
                        upsertCmd.Parameters.AddWithValue("@PrintInBackupReceipt", row["PrintInBackupReceipt"] != DBNull.Value ? row["PrintInBackupReceipt"] : DBNull.Value);
                        upsertCmd.Parameters.AddWithValue("@BranchId", localBranchId > 0 ? (object)localBranchId : DBNull.Value);
                        upsertCmd.Parameters.AddWithValue("@CreationDate", row["CreationDate"] != DBNull.Value ? row["CreationDate"] : DateTime.UtcNow);

                        await upsertCmd.ExecuteNonQueryAsync();
                    }
                }
            }
        }
    }

    private async Task UpsertTableById(SqlConnection hqConn, SqlConnection localConn, SqlTransaction transaction, string tableName, string[] updateCols)
    {
        var checkCmd = new SqlCommand($"SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '{tableName}'", hqConn);
        if (Convert.ToInt32(await checkCmd.ExecuteScalarAsync()) == 0) return;

        var cmd = new SqlCommand($"SELECT * FROM [{tableName}]", hqConn);
        using (var reader = await cmd.ExecuteReaderAsync())
        {
            var dt = new DataTable();
            dt.Load(reader);

            if (!dt.Columns.Contains("Id") || dt.Rows.Count == 0) return;

            var allCols = dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();
            var validUpdateCols = updateCols.Where(c => allCols.Contains(c)).ToList();
            if (validUpdateCols.Count == 0) return;

            var setClause = string.Join(", ", validUpdateCols.Select(c => $"[{c}] = @{c}"));
            var insertCols = string.Join(", ", allCols.Select(c => $"[{c}]"));
            var insertParams = string.Join(", ", allCols.Select(c => $"@{c}"));

            var sql = $@"
IF EXISTS (SELECT 1 FROM [{tableName}] WHERE [Id] = @Id)
BEGIN
    UPDATE [{tableName}] SET {setClause} WHERE [Id] = @Id;
END
ELSE
BEGIN
    SET IDENTITY_INSERT [{tableName}] ON;
    INSERT INTO [{tableName}] ({insertCols}) VALUES ({insertParams});
    SET IDENTITY_INSERT [{tableName}] OFF;
END";

            foreach (DataRow row in dt.Rows)
            {
                using (var upsertCmd = new SqlCommand(sql, localConn, transaction))
                {
                    foreach (var col in allCols)
                    {
                        upsertCmd.Parameters.AddWithValue($"@{col}", row[col] ?? DBNull.Value);
                    }
                    await upsertCmd.ExecuteNonQueryAsync();
                }
            }
        }
    }
}
