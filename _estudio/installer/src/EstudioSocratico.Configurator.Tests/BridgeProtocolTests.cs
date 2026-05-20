using System.Text.Json;
using EstudioSocratico.Configurator.Core;
using Xunit;

namespace EstudioSocratico.Configurator.Tests;

public class BridgeProtocolTests
{
    [Fact]
    public void ParseRequest_AcceptsTypedObjectContract()
    {
        const string json = """
        {
          "id": "req-1",
          "type": "diagnoseEnvironment",
          "payload": {
            "workspace": "C:\\temp\\estudio"
          }
        }
        """;

        var request = BridgeProtocol.ParseRequest(json);

        Assert.Equal("req-1", request.Id);
        Assert.Equal(BridgeAction.DiagnoseEnvironment, request.Action);
        var workspace = Assert.IsType<JsonElement>(request.Payload["workspace"]);
        Assert.Equal(@"C:\temp\estudio", workspace.GetString());
    }

    [Fact]
    public void ParseRequest_AcceptsLegacyStringifiedJson()
    {
        var legacy = JsonSerializer.Serialize("""
        {
          "id": "req-2",
          "action": "createSetupPlan",
          "payload": {}
        }
        """);

        var request = BridgeProtocol.ParseRequest(legacy);

        Assert.Equal("req-2", request.Id);
        Assert.Equal(BridgeAction.CreateSetupPlan, request.Action);
        Assert.Empty(request.Payload);
    }

    [Fact]
    public void CreateSuccessResponse_UsesResultContract()
    {
        var response = BridgeProtocol.CreateSuccessResponse(
            new BridgeRequest
            {
                Id = "req-3",
                Action = BridgeAction.OpenLogs
            },
            new { opened = true });

        Assert.Equal("req-3", response.Id);
        Assert.True(response.Ok);
        Assert.Equal("openLogs.result", response.Type);
        Assert.NotNull(response.Payload);
    }
}
