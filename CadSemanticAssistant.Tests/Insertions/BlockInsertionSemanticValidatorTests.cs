using CadSemanticAssistant.Application.Insertions.Validation;
using CadSemanticAssistant.Domain.Insertions;
using CadSemanticAssistant.Infrastructure.Configuration.Catalog;

namespace CadSemanticAssistant.Tests.Insertions;

public sealed class BlockInsertionSemanticValidatorTests
{
    private static string CatalogPath => Path.Combine(
        AppContext.BaseDirectory, "TestData", "catalogo_bloques.v1.json");
    private static string RulesPath => Path.Combine(
        AppContext.BaseDirectory, "TestData", "reglas_dominio.v1.json");

    private static BlockInsertionSemanticValidator CreateValidator() => new(
        new JsonBlockCatalogProvider(CatalogPath),
        JsonSemanticRuleSettingsLoader.Load(RulesPath));

    [Fact]
    public void ValidRack_IsAccepted()
    {
        SemanticValidationResult result = Validate([Rack("RA-001")]);
        Assert.True(result.IsValid, string.Join("; ", result.Issues.Select(i => i.Message)));
    }

    [Fact]
    public void ValidComplement_AttachedToExistingMachine_IsAccepted()
    {
        SemanticValidationResult result = Validate(
            [Beacon("BE-001", "MA-001")], [ExistingMachine("MA-001")]);
        Assert.True(result.IsValid, string.Join("; ", result.Issues.Select(i => i.Message)));
    }

    [Fact]
    public void Complement_CanReferenceMachineInSameBatch()
    {
        SemanticValidationResult result = Validate(
            [Machine("MA-001"), Beacon("BE-001", "MA-001"), Button("BU-001", "MA-001")]);
        Assert.True(result.IsValid, string.Join("; ", result.Issues.Select(i => i.Message)));
    }

    [Fact]
    public void LegacyExistingWithoutInstanceCode_IsAllowed()
    {
        SemanticValidationResult result = Validate([Rack("RA-001")],
            [new ExistingBlockSnapshot { BlockName = "RACK_SC2_A",
                Attributes = Attributes(("MODEL", "SC2")) }]);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Sem001_UnknownModel_IsBlocked()
    {
        AssertIssue("SEM-001", [Draft("NOT_A_BLOCK", ("INSTANCE_CODE", "RA-001"))]);
    }

    [Fact]
    public void Sem002_MissingRequiredAttribute_IsBlocked()
    {
        AssertIssue("SEM-002", [Draft("RACK_SC2_A", ("INSTANCE_CODE", "RA-001"),
            ("MODEL", "SC2"), ("HEIGHT", "9000"))]);
    }

    [Fact]
    public void Sem002_WhitespaceRequiredAttribute_IsBlocked()
    {
        AssertIssue("SEM-002", [Rack("   ")]);
    }

    [Theory]
    [InlineData("4.5")]
    [InlineData("abc")]
    [InlineData("NaN")]
    public void Sem002_InvalidInteger_IsBlocked(string invalid)
    {
        AssertIssue("SEM-002", [Rack("RA-001", capacity: invalid)]);
    }

    [Fact]
    public void Sem003_FixedModelViolation_IsBlocked()
    {
        AssertIssue("SEM-003", [Rack("RA-001", model: "SC1")]);
    }

    [Fact]
    public void Sem003_OutOfRangeHeight_IsBlocked()
    {
        AssertIssue("SEM-003", [Rack("RA-001", height: "19000")]);
    }

    [Fact]
    public void Sem003_HeightBoundary_IsAllowed()
    {
        Assert.True(Validate([Rack("RA-001", height: "8000")]).IsValid);
        Assert.True(Validate([Rack("RA-001", height: "18000")]).IsValid);
    }

    [Fact]
    public void Sem003_UnknownAttribute_IsBlocked()
    {
        AssertIssue("SEM-003", [Draft("RACK_SC2_A", ("INSTANCE_CODE", "RA-001"),
            ("MODEL", "SC2"), ("HEIGHT", "9000"), ("CAPACITY", "10"),
            ("UNKNOWN", "value"))]);
    }

    [Fact]
    public void Sem003_InvalidPattern_IsBlocked()
    {
        AssertIssue("SEM-003", [Rack("RACK-001")]); // 4 letters not allowed by catalog
    }

    [Fact]
    public void Sem003_DisallowedComplementValue_IsBlocked()
    {
        AssertIssue("SEM-003", [Draft("BEACON_A", ("INSTANCE_CODE", "BE-001"),
            ("PARENT_CODE", "MA-001"), ("COLOR", "Blue"), ("MODE", "Steady"))],
            [ExistingMachine("MA-001")]);
    }

    [Fact]
    public void Sem004_DuplicateCodeAgainstExisting_IgnoresCase()
    {
        AssertIssue("SEM-004", [Rack("RA-001")], [ExistingRack("ra-001")]);
    }

    [Fact]
    public void Sem004_DuplicateWithinProposals_IsBlocked()
    {
        AssertIssue("SEM-004", [Rack("RA-001"), Rack("ra-001")]);
    }

    [Fact]
    public void Sem004_PreexistingDuplicateCodes_AreReported()
    {
        AssertIssue("SEM-004", [Rack("RA-003")],
            [ExistingRack("RA-001"), ExistingRack("ra-001")]);
    }

    [Fact]
    public void Sem005_EmptyBatch_IsBlocked()
    {
        AssertIssue("SEM-005", []);
    }

    [Fact]
    public void Sem005_MoreThanSix_IsBlocked()
    {
        AssertIssue("SEM-005", Enumerable.Range(1, 7)
            .Select(i => Rack($"RA-{i:000}")).ToArray());
    }

    [Fact]
    public void Sem005_SixInstances_AreAccepted()
    {
        Assert.True(Validate(Enumerable.Range(1, 6)
            .Select(i => Rack($"RA-{i:000}")).ToArray()).IsValid);
    }

    [Fact]
    public void Sem006_UnknownParent_IsBlocked()
    {
        AssertIssue("SEM-006", [Beacon("BE-001", "MA-999")]);
    }

    [Fact]
    public void Sem006_RackCannotBeParent()
    {
        AssertIssue("SEM-006", [Beacon("BE-001", "RA-001")], [ExistingRack("RA-001")]);
    }

    [Fact]
    public void Sem006_ExistingMachineWithWrongModel_IsNotTrusted()
    {
        AssertIssue("SEM-006", [Beacon("BE-001", "MA-001")],
            [new ExistingBlockSnapshot { BlockName = "MACHINE_A", Attributes =
                Attributes(("INSTANCE_CODE", "MA-001"), ("MODEL", "MX300")) }]);
    }

    [Fact]
    public void Sem007_IncompatibleButtonParent_IsBlocked()
    {
        AssertIssue("SEM-007", [Button("BU-001", "MA-001")],
            [ExistingMachine("MA-001", "MACHINE_C", "MX300")]);
    }

    [Fact]
    public void Sem008_SecondBeaconOnSameMachine_IsBlocked()
    {
        AssertIssue("SEM-008", [Beacon("BE-002", "MA-001")],
            [ExistingMachine("MA-001"), ExistingBeacon("BE-001", "MA-001")]);
    }

    [Fact]
    public void Sem008_SecondBeaconWithinBatch_IsBlocked()
    {
        AssertIssue("SEM-008", [Machine("MA-001"), Beacon("BE-001", "MA-001"),
            Beacon("BE-002", "MA-001")]);
    }

    [Fact]
    public void Sem008_OneOfEachComplementType_IsAllowed()
    {
        Assert.True(Validate([Beacon("BE-001", "MA-001"), Button("BU-001", "MA-001")],
            [ExistingMachine("MA-001")]).IsValid);
    }

    [Fact]
    public void MixedCaseAttributeNames_AreAccepted()
    {
        Assert.True(Validate([Draft("RACK_SC2_A", ("instance_code", "RA-001"),
            ("model", "SC2"), ("height", "9000"), ("capacity", "10"))]).IsValid);
    }

    [Fact]
    public void DuplicateAliasAttribute_IsRejected()
    {
        AssertIssue("SEM-003", [Draft("RACK_SC2_A", ("INSTANCE_CODE", "RA-001"),
            ("instance_code", "RA-002"), ("MODEL", "SC2"),
            ("HEIGHT", "9000"), ("CAPACITY", "10"))]);
    }

    private static void AssertIssue(string code,
        IReadOnlyList<BlockInsertionDraft> proposed,
        IReadOnlyList<ExistingBlockSnapshot>? existing = null)
    {
        SemanticValidationResult result = Validate(proposed, existing);
        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, issue => issue.RuleCode == code);
    }

    private static SemanticValidationResult Validate(
        IReadOnlyList<BlockInsertionDraft> proposed,
        IReadOnlyList<ExistingBlockSnapshot>? existing = null) =>
        CreateValidator().Validate(new BlockInsertionBatch
        {
            Proposed = proposed,
            Existing = existing ?? []
        });

    private static Dictionary<string, string?> Attributes(params (string key, string? value)[] entries)
    {
        // Ordinal dictionary intentionally preserves case aliases for one test.
        Dictionary<string, string?> values = new(StringComparer.Ordinal);
        foreach ((string key, string? value) in entries)
        {
            values.Add(key, value);
        }
        return values;
    }

    private static BlockInsertionDraft Draft(string block, params (string key, string? value)[] values) =>
        new() { BlockName = block, Attributes = Attributes(values) };

    private static BlockInsertionDraft Rack(string code, string model = "SC2",
        string height = "9000", string capacity = "10") =>
        Draft("RACK_SC2_A", ("INSTANCE_CODE", code), ("MODEL", model),
            ("HEIGHT", height), ("CAPACITY", capacity));

    private static BlockInsertionDraft Machine(string code, string blockName = "MACHINE_A",
        string model = "MX100", string type = "Miniload") =>
        Draft(blockName, ("INSTANCE_CODE", code), ("MODEL", model), ("TYPE", type));

    private static BlockInsertionDraft Beacon(string code, string parentCode) =>
        Draft("BEACON_A", ("INSTANCE_CODE", code), ("PARENT_CODE", parentCode),
            ("COLOR", "Green"), ("MODE", "Steady"));

    private static BlockInsertionDraft Button(string code, string parentCode) =>
        Draft("BUTTON_STATION_A", ("INSTANCE_CODE", code), ("PARENT_CODE", parentCode),
            ("BUTTONS", "2"), ("FUNCTION", "StartStop"));

    private static ExistingBlockSnapshot ExistingMachine(string code,
        string blockName = "MACHINE_A", string model = "MX100") =>
        new()
        {
            BlockName = blockName,
            Attributes = Attributes(("INSTANCE_CODE", code), ("MODEL", model))
        };

    private static ExistingBlockSnapshot ExistingRack(string code) =>
        new()
        {
            BlockName = "RACK_SC2_A",
            Attributes = Attributes(
            ("INSTANCE_CODE", code), ("MODEL", "SC2"))
        };

    private static ExistingBlockSnapshot ExistingBeacon(string code, string parentCode) =>
        new()
        {
            BlockName = "BEACON_A",
            Attributes = Attributes(
            ("INSTANCE_CODE", code), ("PARENT_CODE", parentCode))
        };
}
