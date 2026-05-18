namespace Estudio.Setup.Core;

public sealed record SetupRepairAction(
    string Id,
    string HumanMessage,
    string TechnicalMessage);