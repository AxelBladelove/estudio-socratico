function describeAiTestGeneration(config) {
  if (!config.features?.aiGeneratedTests) {
    return [
      "Generar tests de logica con IA es experimental y esta apagado.",
      "Activalo en usuario/config/estudio-socratico.extension.local.json con features.aiGeneratedTests=true.",
      "La salida futura ira a una carpeta local ignorada por Git, sin cambiar F9 ni revelar soluciones.",
    ].join("\n");
  }

  return [
    "Base 2.5: feature flag activo, pero el generador completo aun no escribe tests.",
    "Diseno: leer el .c activo, pedir casos logicos al AiProvider, escribirlos en .estudio-tests/ y regenerarlos bajo demanda.",
    "Reglas: no resolver el ejercicio, no tocar tests oficiales de Exercism y no ensuciar el repo versionado.",
  ].join("\n");
}

module.exports = {
  describeAiTestGeneration,
};
