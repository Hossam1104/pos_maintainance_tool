using System.Text.Json;
using PosAdminTool.Agent.IntegrationTests.TestSupport;
using PosAdminTool.Contracts.V1.Files;
using PosAdminTool.Contracts.V1.Session;

namespace PosAdminTool.Agent.IntegrationTests;

/// <summary>
/// Snapshot tests proving contracts serialize with the intended casing, UTC timestamp format, and
/// string (not numeric) enum representation — the same <see cref="JsonSerializerOptions"/> shape as
/// the Agent's ConfigureHttpJsonOptions (plan section 5.1, Session 02 task 4/9).
/// </summary>
public class ContractSerializationTests
{
    [Fact]
    public void SessionInfoDto_SerializesWithCamelCaseProperties()
    {
        var dto = new SessionInfoDto(
            PrincipalName: "TESTDOMAIN\\admin-user",
            IsAuthorized: true,
            AgentVersion: "1.0.0.0",
            ApiVersion: "1.0",
            SupportedApiVersions: ["1.0"]);

        var json = JsonSerializer.Serialize(dto, TestJsonOptions.Default);

        Assert.Equal(
            """{"principalName":"TESTDOMAIN\\admin-user","isAuthorized":true,"agentVersion":"1.0.0.0","apiVersion":"1.0","supportedApiVersions":["1.0"]}""",
            json);
    }

    [Fact]
    public void FileHandleDto_SerializesEnumAsCamelCaseStringAndTimestampAsUtc()
    {
        var expiresAtUtc = new DateTimeOffset(2026, 3, 1, 12, 30, 0, TimeSpan.Zero);
        var dto = new FileHandleDto("a1b2c3", FileHandlePurpose.RestoreSource, expiresAtUtc);

        var json = JsonSerializer.Serialize(dto, TestJsonOptions.Default);

        Assert.Equal(
            """{"handleId":"a1b2c3","purpose":"restoreSource","expiresAtUtc":"2026-03-01T12:30:00+00:00"}""",
            json);
    }

    [Fact]
    public void FileBrowseEntryDto_NeverSerializesAnAbsolutePathShapedValue()
    {
        var dto = new FileBrowseEntryDto("backup.bak", IsDirectory: false, RelativeSubPath: "sub/backup.bak", SizeBytes: 1024, LastModifiedUtc: DateTimeOffset.UtcNow);

        var json = JsonSerializer.Serialize(dto, TestJsonOptions.Default);

        Assert.DoesNotContain(":\\\\", json);
        Assert.DoesNotContain("C:\\", json, StringComparison.OrdinalIgnoreCase);
    }
}
