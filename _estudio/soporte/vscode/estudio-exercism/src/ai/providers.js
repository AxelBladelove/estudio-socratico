const { GeminiProvider } = require("./geminiProvider");
const { OpenCodeProvider } = require("./opencodeProvider");

function createAiProvider(config, options = {}) {
  const providerName = String(config?.experimental?.aiExerciseAnalysisProvider || config?.provider || "gemini").toLowerCase();
  if (providerName === "opencode") {
    return new OpenCodeProvider(config, options.execFile);
  }
  return new GeminiProvider(config, options.fetch);
}

module.exports = {
  createAiProvider,
};
