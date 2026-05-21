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

    [Fact]
    public void ApplyWorkflow_Payload_Carries_SetupMode()
    {
        const string json = """
        {
          "id": "req-4",
          "type": "ApplyWorkflow",
          "payload": {
            "mode": "Reinstall",
            "workspacePath": "C:\\Estudio\\repo",
            "allowAggressiveCleanup": false
          }
        }
        """;

        var request = BridgeProtocol.ParseRequest(json);
        var setupRequest = BridgePayload.ToSetupRequest(request);

        Assert.Equal(BridgeAction.ApplyWorkflow, request.Action);
        Assert.Equal(SetupMode.Reinstall, setupRequest.Mode);
        Assert.Equal(@"C:\Estudio\repo", setupRequest.WorkspacePath);
        Assert.False(setupRequest.AllowAggressiveCleanup);
    }

    [Fact]
    public void ApplyWorkflow_Accepts_Setup_As_Install_Mode()
    {
        const string json = """
        {
          "id": "req-5",
          "type": "ApplyWorkflow",
          "payload": {
            "mode": "setup"
          }
        }
        """;

        var request = BridgeProtocol.ParseRequest(json);
        var setupRequest = BridgePayload.ToSetupRequest(request);

        Assert.Equal(SetupMode.Install, setupRequest.Mode);
    }

    [Fact]
    public void ApplyWorkflow_Accepts_Update_Mode()
    {
        const string json = """
        {
          "id": "req-update",
          "type": "ApplyWorkflow",
          "payload": {
            "mode": "actualizar"
          }
        }
        """;

        var request = BridgeProtocol.ParseRequest(json);
        var setupRequest = BridgePayload.ToSetupRequest(request);

        Assert.Equal(SetupMode.Update, setupRequest.Mode);
    }

    [Fact]
    public void Uninstall_Defaults_To_Real_Apply_Not_DryRun()
    {
        const string json = """
        {
          "id": "req-uninstall",
          "type": "UninstallManaged",
          "payload": {
            "mode": "uninstall",
            "dryRun": false
          }
        }
        """;

        var request = BridgeProtocol.ParseRequest(json);
        var setupRequest = BridgePayload.ToSetupRequest(request, SetupMode.Uninstall);

        Assert.Equal(SetupMode.Uninstall, setupRequest.Mode);
        Assert.False(setupRequest.UninstallDryRun);
    }

    [Fact]
    public void ApplyWorkflow_ReceivesLocalAlias()
    {
        const string json = """
        {
          "id": "req-5b",
          "type": "ApplyWorkflow",
          "payload": {
            "mode": "repair",
            "localAlias": "axel"
          }
        }
        """;

        var request = BridgeProtocol.ParseRequest(json);
        var setupRequest = BridgePayload.ToSetupRequest(request);

        Assert.Equal("axel", setupRequest.LocalAlias);
    }

    [Fact]
    public void ParseRequest_Rejects_Unknown_Action()
    {
        const string json = """
        {
          "id": "req-6",
          "type": "DeleteEverything",
          "payload": {}
        }
        """;

        Assert.Throws<InvalidOperationException>(() => BridgeProtocol.ParseRequest(json));
    }
}
