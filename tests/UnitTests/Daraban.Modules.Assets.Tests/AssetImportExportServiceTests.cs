using System.Text;
using Daraban.Modules.Assets.Data.Entities;
using Daraban.Modules.Assets.Data.Repositories;
using Daraban.Modules.Assets.Services;
using Moq;
using Xunit;

namespace Daraban.Modules.Assets.Tests;

/// <summary>
/// AssetImportService and AssetExportService driven with real in-memory CSV/XLSX streams.
/// These are the only Assets services with non-trivial parsing, so the tests feed them actual
/// file bytes rather than mocking the parse away — a malformed date or an unknown asset type has
/// to produce a row-level error, not an exception that kills the whole upload.
/// </summary>
public class AssetImportExportServiceTests
{
    private static readonly Guid EntityId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ActorId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly Mock<IAssetRepository> _assets = new(MockBehavior.Strict);
    private readonly Mock<IAssetTypeRepository> _assetTypes = new(MockBehavior.Strict);

    private AssetImportService CreateImportSut() => new(_assets.Object, _assetTypes.Object);
    private AssetExportService CreateExportSut() => new(_assets.Object);

    private static AssetType Laptop() => new()
    {
        Id = Guid.CreateVersion7(),
        CategoryId = Guid.CreateVersion7(),
        Name = "Laptop",
    };

    private static Stream Csv(string content)
        => new MemoryStream(new UTF8Encoding(false).GetBytes(content));

    private const string Header =
        "Name,AssetType,AssetTag,SerialNumber,Status,PurchaseDate,PurchaseCost,PurchaseCurrency,WarrantyExpiry,OrderNumber,SupplierName,Notes";

    /// <summary>Asset types are pre-loaded once for name -> id resolution.</summary>
    private void ArrangeAssetTypes(params AssetType[] types)
        => _assetTypes.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(types);

    // ---- File format handling ----------------------------------------------------------------

    [Theory]
    [InlineData("assets.txt")]
    [InlineData("assets.json")]
    [InlineData("assets")]
    [InlineData("assets.xls")] // legacy Excel is NOT supported -- only .xlsx
    public async Task ImportAsync_Rejects_Unsupported_Extensions(string fileName)
    {
        var result = await CreateImportSut().ImportAsync(Csv(Header), fileName, EntityId, ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("ASSETS.IMPORT_INVALID_FILE", result.Error!.Code);
    }

    [Fact]
    public async Task ImportAsync_Rejects_A_Header_Only_File()
    {
        var result = await CreateImportSut().ImportAsync(Csv(Header + "\n"), "assets.csv", EntityId, ActorId);

        Assert.False(result.IsSuccess);
        Assert.Equal("ASSETS.IMPORT_EMPTY_FILE", result.Error!.Code);
    }

    [Fact]
    public async Task ImportAsync_Accepts_Uppercase_Extensions()
    {
        ArrangeAssetTypes(Laptop());
        _assets.Setup(r => r.AddAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _assets.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var csv = $"{Header}\nDell XPS,Laptop,,,,,,,,,,\n";
        var result = await CreateImportSut().ImportAsync(Csv(csv), "ASSETS.CSV", EntityId, ActorId);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.SuccessCount);
    }

    // ---- Row validation ----------------------------------------------------------------------

    [Fact]
    public async Task ImportAsync_Reports_A_Missing_Name_As_A_Row_Error()
    {
        ArrangeAssetTypes(Laptop());

        var csv = $"{Header}\n,Laptop,,,,,,,,,,\n";
        var result = await CreateImportSut().ImportAsync(Csv(csv), "assets.csv", EntityId, ActorId);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.ErrorCount);
        Assert.Equal(0, result.Value.SuccessCount);
        Assert.Contains("Name is required.", result.Value.Rows[0].Errors);

        // Row 2 -- the data starts after the header.
        Assert.Equal(2, result.Value.Rows[0].RowNumber);
    }

    [Fact]
    public async Task ImportAsync_Reports_An_Unknown_AssetType_As_A_Row_Error()
    {
        ArrangeAssetTypes(Laptop());

        var csv = $"{Header}\nDell XPS,Toaster,,,,,,,,,,\n";
        var result = await CreateImportSut().ImportAsync(Csv(csv), "assets.csv", EntityId, ActorId);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.ErrorCount);
        Assert.Contains(result.Value.Rows[0].Errors, e => e.Contains("Toaster"));
    }

    [Fact]
    public async Task ImportAsync_Resolves_AssetType_Names_Case_Insensitively()
    {
        ArrangeAssetTypes(Laptop());
        _assets.Setup(r => r.AddAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _assets.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var csv = $"{Header}\nDell XPS,LAPTOP,,,,,,,,,,\n";
        var result = await CreateImportSut().ImportAsync(Csv(csv), "assets.csv", EntityId, ActorId);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.SuccessCount);
    }

    [Fact]
    public async Task ImportAsync_Ignores_SoftDeleted_AssetTypes()
    {
        var deleted = Laptop();
        deleted.DeletedAt = DateTimeOffset.UtcNow;
        ArrangeAssetTypes(deleted);

        var csv = $"{Header}\nDell XPS,Laptop,,,,,,,,,,\n";
        var result = await CreateImportSut().ImportAsync(Csv(csv), "assets.csv", EntityId, ActorId);

        // A retired asset type must not silently absorb new imports.
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.ErrorCount);
    }

    [Theory]
    [InlineData("NotAStatus", "Invalid Status")]
    [InlineData("32-13-2020", "Invalid PurchaseDate")]
    public async Task ImportAsync_Reports_Malformed_Field_Values(string badValue, string expectedFragment)
    {
        ArrangeAssetTypes(Laptop());

        // Column order: Name,AssetType,AssetTag,SerialNumber,Status,PurchaseDate,...
        var csv = expectedFragment.Contains("Status")
            ? $"{Header}\nDell XPS,Laptop,,,{badValue},,,,,,,\n"
            : $"{Header}\nDell XPS,Laptop,,,,{badValue},,,,,,\n";

        var result = await CreateImportSut().ImportAsync(Csv(csv), "assets.csv", EntityId, ActorId);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.ErrorCount);
        Assert.Contains(result.Value.Rows[0].Errors, e => e.Contains(expectedFragment));
    }

    [Fact]
    public async Task ImportAsync_Reports_A_NonNumeric_PurchaseCost()
    {
        ArrangeAssetTypes(Laptop());

        var csv = $"{Header}\nDell XPS,Laptop,,,,,abc,,,,,\n";
        var result = await CreateImportSut().ImportAsync(Csv(csv), "assets.csv", EntityId, ActorId);

        Assert.True(result.IsSuccess);
        Assert.Contains(result.Value.Rows[0].Errors, e => e.Contains("Invalid PurchaseCost"));
    }

    [Fact]
    public async Task ImportAsync_Collects_Every_Error_On_A_Row_Rather_Than_Stopping_At_The_First()
    {
        ArrangeAssetTypes(Laptop());

        // Blank name AND unknown type AND bad status.
        var csv = $"{Header}\n,Toaster,,,Nonsense,,,,,,,\n";
        var result = await CreateImportSut().ImportAsync(Csv(csv), "assets.csv", EntityId, ActorId);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Rows[0].Errors.Count >= 3);
    }

    [Fact]
    public async Task ImportAsync_Continues_Past_A_Bad_Row()
    {
        ArrangeAssetTypes(Laptop());
        _assets.Setup(r => r.AddAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _assets.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var csv = $"{Header}\n,Laptop,,,,,,,,,,\nGood One,Laptop,,,,,,,,,,\n";
        var result = await CreateImportSut().ImportAsync(Csv(csv), "assets.csv", EntityId, ActorId);

        // One bad row must not abort the batch -- partial success is the point of the row report.
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.TotalRows);
        Assert.Equal(1, result.Value.SuccessCount);
        Assert.Equal(1, result.Value.ErrorCount);
    }

    // ---- Dry run -----------------------------------------------------------------------------

    [Fact]
    public async Task ImportAsync_DryRun_Validates_Without_Writing_Anything()
    {
        ArrangeAssetTypes(Laptop());

        var csv = $"{Header}\nDell XPS,Laptop,AT-1,SN-1,,,,,,,,\n";
        var result = await CreateImportSut().ImportAsync(
            Csv(csv), "assets.csv", EntityId, ActorId, dryRun: true);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.DryRun);
        Assert.Equal(1, result.Value.SuccessCount);

        // Strict mocks make this airtight: no Add, no Save, and not even the duplicate lookups.
        _assets.Verify(r => r.AddAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>()), Times.Never);
        _assets.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ImportAsync_DryRun_Still_Reports_Validation_Errors()
    {
        ArrangeAssetTypes(Laptop());

        var csv = $"{Header}\n,Toaster,,,,,,,,,,\n";
        var result = await CreateImportSut().ImportAsync(
            Csv(csv), "assets.csv", EntityId, ActorId, dryRun: true);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.ErrorCount);
    }

    // ---- Duplicate detection -----------------------------------------------------------------

    [Fact]
    public async Task ImportAsync_Rejects_A_Row_Whose_AssetTag_Already_Exists_In_The_Database()
    {
        ArrangeAssetTypes(Laptop());
        _assets.Setup(r => r.AssetTagExistsAsync("AT-1", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var csv = $"{Header}\nDell XPS,Laptop,AT-1,,,,,,,,,\n";
        var result = await CreateImportSut().ImportAsync(Csv(csv), "assets.csv", EntityId, ActorId);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.ErrorCount);
        Assert.Contains("AssetTag already exists.", result.Value.Rows[0].Errors);
    }

    [Fact]
    public async Task ImportAsync_Catches_Duplicate_AssetTags_Within_The_Same_File()
    {
        ArrangeAssetTypes(Laptop());
        _assets.Setup(r => r.AssetTagExistsAsync("AT-1", null, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _assets.Setup(r => r.AddAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _assets.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var csv = $"{Header}\nFirst,Laptop,AT-1,,,,,,,,,\nSecond,Laptop,AT-1,,,,,,,,,\n";
        var result = await CreateImportSut().ImportAsync(Csv(csv), "assets.csv", EntityId, ActorId);

        // The database check alone can't catch this -- neither row is committed yet, so the
        // service tracks tags seen within the batch.
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.SuccessCount);
        Assert.Equal(1, result.Value.ErrorCount);
    }

    [Fact]
    public async Task ImportAsync_Catches_Duplicate_SerialNumbers_Within_The_Same_File()
    {
        ArrangeAssetTypes(Laptop());
        _assets.Setup(r => r.SerialNumberExistsAsync("SN-1", null, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _assets.Setup(r => r.AddAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _assets.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var csv = $"{Header}\nFirst,Laptop,,SN-1,,,,,,,,\nSecond,Laptop,,SN-1,,,,,,,,\n";
        var result = await CreateImportSut().ImportAsync(Csv(csv), "assets.csv", EntityId, ActorId);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.SuccessCount);
        Assert.Contains("SerialNumber already exists.", result.Value.Rows[1].Errors);
    }

    [Fact]
    public async Task ImportAsync_Does_Not_Save_When_Every_Row_Failed()
    {
        ArrangeAssetTypes(Laptop());

        var csv = $"{Header}\n,Toaster,,,,,,,,,,\n,Toaster,,,,,,,,,,\n";
        var result = await CreateImportSut().ImportAsync(Csv(csv), "assets.csv", EntityId, ActorId);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.SuccessCount);
        _assets.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- Field mapping -----------------------------------------------------------------------

    [Fact]
    public async Task ImportAsync_Maps_Every_Column_And_Stamps_The_Tenant()
    {
        var type = Laptop();
        ArrangeAssetTypes(type);

        Asset? captured = null;
        _assets.Setup(r => r.AssetTagExistsAsync("AT-1", null, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _assets.Setup(r => r.SerialNumberExistsAsync("SN-1", null, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _assets.Setup(r => r.AddAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>()))
            .Callback<Asset, CancellationToken>((a, _) => captured = a)
            .Returns(Task.CompletedTask);
        _assets.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var csv = $"{Header}\nDell XPS,Laptop,AT-1,SN-1,InUse,2024-01-15,1299.50,EUR,2027-01-15,PO-77,Acme,Spare\n";
        var result = await CreateImportSut().ImportAsync(Csv(csv), "assets.csv", EntityId, ActorId);

        Assert.True(result.IsSuccess);
        Assert.Equal("Dell XPS", captured!.Name);
        Assert.Equal(type.Id, captured.AssetTypeId);
        Assert.Equal(AssetStatus.InUse, captured.Status);
        Assert.Equal(new DateOnly(2024, 1, 15), captured.PurchaseDate);
        Assert.Equal(1299.50m, captured.PurchaseCost);
        Assert.Equal("EUR", captured.PurchaseCurrency);
        Assert.Equal(new DateOnly(2027, 1, 15), captured.WarrantyExpiry);
        Assert.Equal("PO-77", captured.OrderNumber);
        Assert.Equal("Acme", captured.SupplierName);

        // The tenant comes from the caller's context, never from the file.
        Assert.Equal(EntityId, captured.EntityNodeId);
    }

    [Fact]
    public async Task ImportAsync_Defaults_A_Blank_Status_To_InStock()
    {
        ArrangeAssetTypes(Laptop());

        Asset? captured = null;
        _assets.Setup(r => r.AddAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>()))
            .Callback<Asset, CancellationToken>((a, _) => captured = a)
            .Returns(Task.CompletedTask);
        _assets.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var csv = $"{Header}\nDell XPS,Laptop,,,,,,,,,,\n";
        await CreateImportSut().ImportAsync(Csv(csv), "assets.csv", EntityId, ActorId);

        Assert.Equal(AssetStatus.InStock, captured!.Status);
    }

    // ---- Template ----------------------------------------------------------------------------

    [Fact]
    public void GetTemplate_Returns_A_Readable_Csv_Header_Row()
    {
        using var stream = CreateImportSut().GetTemplate();

        // Returned rewound so the caller can stream it straight to the response.
        Assert.Equal(0, stream.Position);

        using var reader = new StreamReader(stream);
        var header = reader.ReadLine();

        Assert.NotNull(header);
        foreach (var column in new[] { "Name", "AssetType", "AssetTag", "SerialNumber", "Status" })
            Assert.Contains(column, header);
    }

    [Fact]
    public async Task GetTemplate_Columns_Match_What_Import_Actually_Accepts()
    {
        // Round trip: feed the template's own header back through the importer with one data row.
        // If the two ever drift, downloading the template and filling it in would stop working.
        using var template = CreateImportSut().GetTemplate();
        using var reader = new StreamReader(template);
        var header = await reader.ReadLineAsync();

        ArrangeAssetTypes(Laptop());
        _assets.Setup(r => r.AddAsync(It.IsAny<Asset>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _assets.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var csv = $"{header}\nDell XPS,Laptop,,,,,,,,,,\n";
        var result = await CreateImportSut().ImportAsync(Csv(csv), "assets.csv", EntityId, ActorId);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.SuccessCount);
    }

    // ---- Export ------------------------------------------------------------------------------

    private static Asset ExportableAsset(string name = "Dell XPS") => new()
    {
        Id = Guid.CreateVersion7(),
        AssetTypeId = Guid.CreateVersion7(),
        EntityNodeId = EntityId,
        Name = name,
        AssetTag = "AT-1",
        SerialNumber = "SN-1",
        Status = AssetStatus.InUse,
        AssetType = new AssetType { Id = Guid.CreateVersion7(), Name = "Laptop" },
        PurchaseDate = new DateOnly(2024, 1, 15),
        PurchaseCost = 1299.50m,
        PurchaseCurrency = "EUR",
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task ExportAsync_Csv_Writes_A_Header_And_One_Row_Per_Asset()
    {
        _assets.Setup(r => r.GetPagedAsync(
                EntityId, null, null, null, null, 1, int.MaxValue, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { ExportableAsset("First"), ExportableAsset("Second") }, 2));

        var result = await CreateExportSut().ExportAsync(EntityId, "csv", null, null, null, null);

        Assert.True(result.IsSuccess);
        Assert.Equal("text/csv", result.Value.ContentType);
        Assert.Equal("assets.csv", result.Value.FileName);

        using var reader = new StreamReader(result.Value.Stream);
        var text = await reader.ReadToEndAsync();

        Assert.Contains("Name", text);
        Assert.Contains("First", text);
        Assert.Contains("Second", text);

        // The navigation property is flattened to its name, not serialized as an object.
        Assert.Contains("Laptop", text);
        Assert.Contains("InUse", text);
    }

    [Fact]
    public async Task ExportAsync_Xlsx_Returns_The_Excel_ContentType_And_A_NonEmpty_Workbook()
    {
        _assets.Setup(r => r.GetPagedAsync(
                EntityId, null, null, null, null, 1, int.MaxValue, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { ExportableAsset() }, 1));

        var result = await CreateExportSut().ExportAsync(EntityId, "xlsx", null, null, null, null);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            result.Value.ContentType);
        Assert.Equal("assets.xlsx", result.Value.FileName);
        Assert.True(result.Value.Stream.Length > 0);
        Assert.Equal(0, result.Value.Stream.Position);
    }

    [Theory]
    [InlineData("XLSX")]
    [InlineData("xlsx")]
    public async Task ExportAsync_Matches_The_Format_Case_Insensitively(string format)
    {
        _assets.Setup(r => r.GetPagedAsync(
                EntityId, null, null, null, null, 1, int.MaxValue, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { ExportableAsset() }, 1));

        var result = await CreateExportSut().ExportAsync(EntityId, format, null, null, null, null);

        Assert.True(result.IsSuccess);
        Assert.Equal("assets.xlsx", result.Value.FileName);
    }

    [Theory]
    [InlineData("pdf")]
    [InlineData("")]
    [InlineData("json")]
    public async Task ExportAsync_Falls_Back_To_Csv_For_An_Unrecognised_Format(string format)
    {
        _assets.Setup(r => r.GetPagedAsync(
                EntityId, null, null, null, null, 1, int.MaxValue, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new[] { ExportableAsset() }, 1));

        var result = await CreateExportSut().ExportAsync(EntityId, format, null, null, null, null);

        // Documented behaviour: anything that isn't xlsx yields CSV rather than an error.
        Assert.True(result.IsSuccess);
        Assert.Equal("assets.csv", result.Value.FileName);
    }

    [Fact]
    public async Task ExportAsync_Applies_The_Same_Status_Filter_As_The_List_Endpoint()
    {
        _assets.Setup(r => r.GetPagedAsync(
                EntityId, AssetStatus.Retired, null, null, null, 1, int.MaxValue, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<Asset>(), 0));

        var result = await CreateExportSut().ExportAsync(EntityId, "csv", "retired", null, null, null);

        Assert.True(result.IsSuccess);
        _assets.Verify(r => r.GetPagedAsync(
            EntityId, AssetStatus.Retired, null, null, null, 1, int.MaxValue,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExportAsync_Handles_An_Empty_Result_Set()
    {
        _assets.Setup(r => r.GetPagedAsync(
                EntityId, null, null, null, null, 1, int.MaxValue, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Array.Empty<Asset>(), 0));

        var result = await CreateExportSut().ExportAsync(EntityId, "csv", null, null, null, null);

        Assert.True(result.IsSuccess);

        using var reader = new StreamReader(result.Value.Stream);
        var text = await reader.ReadToEndAsync();

        // Header only -- an export with no matches is still a valid file, not a failure.
        Assert.Contains("Name", text);
    }
}
