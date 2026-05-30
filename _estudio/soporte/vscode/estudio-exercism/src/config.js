const fs = require("fs");
const path = require("path");

const DEFAULT_EXTENSION_CONFIG = {
  provider: "gemini",
  apiKey: "",
  model: "gemini-2.5-flash",
  features: {
    translateIntroductions: true,
    importExercism: true,
    importAlejandroGists: true,
    aiExerciseAnalysis: false,
    opencodeIntegration: false,
    aiGeneratedTests: false,
    autoGitSync: false,
  },
  experimental: {
    aiExerciseAnalysisProvider: "gemini",
    opencodeCommand: "opencode",
    preferredFreeModelStrategy: "best-for-c-exercises",
    applyHeaderPatchesAutomatically: false,
    appendInstructionHintsAutomatically: false,
  },
  ai: {
    mode: "opencode",
    fallbackMode: "direct",
    taskRouting: {
      translation: "direct-or-opencode-fast",
      exerciseMetadata: "opencode-best",
      quizGeneration: "opencode-best-reasoning",
      errorExplanation: "opencode-fast",
      testAdapter: "opencode-code",
    },
  },
  directProviders: {
    google: {
      apiKey: "",
      model: "gemini-2.5-flash",
    },
    openai: {
      apiKey: "",
      model: "gpt-5-mini",
    },
    anthropic: {
      apiKey: "",
      model: "claude-haiku-4.5",
    },
  },
  opencode: {
    enabled: true,
    command: "opencode",
    preferredFreeModelStrategy: "best-for-learning-c",
    allowCopilotProvider: true,
    allowLocalModels: true,
  },
};

function getConfigDirectory(root) {
  return path.join(root, "usuario", "config");
}

function getLocalConfigPath(root) {
  return path.join(getConfigDirectory(root), "estudio-socratico.extension.local.json");
}

function getExampleConfigPath(root) {
  return path.join(getConfigDirectory(root), "estudio-socratico.extension.example.json");
}

function getDefaultExtensionConfigText() {
  return `${JSON.stringify(DEFAULT_EXTENSION_CONFIG, null, 2)}\n`;
}

function ensureExtensionConfigFiles(root) {
  const configDir = getConfigDirectory(root);
  const localConfigPath = getLocalConfigPath(root);
  const exampleConfigPath = getExampleConfigPath(root);
  fs.mkdirSync(configDir, { recursive: true });
  if (!fs.existsSync(exampleConfigPath)) {
    fs.writeFileSync(exampleConfigPath, getDefaultExtensionConfigText(), "utf8");
  }
  if (!fs.existsSync(localConfigPath)) {
    fs.writeFileSync(localConfigPath, getDefaultExtensionConfigText(), "utf8");
  } else {
    completeLocalConfig(localConfigPath);
  }
  return { localConfigPath, exampleConfigPath };
}

function completeLocalConfig(localConfigPath) {
  try {
    const current = JSON.parse(fs.readFileSync(localConfigPath, "utf8"));
    const merged = mergeMissing(current, DEFAULT_EXTENSION_CONFIG);
    if (JSON.stringify(current) !== JSON.stringify(merged)) {
      fs.writeFileSync(localConfigPath, `${JSON.stringify(merged, null, 2)}\n`, "utf8");
    }
  } catch {
    // Do not overwrite a malformed local file; it may contain a key the user is repairing.
  }
}

function readExtensionConfig(root) {
  const { localConfigPath, exampleConfigPath } = ensureExtensionConfigFiles(root);
  try {
    const config = JSON.parse(fs.readFileSync(localConfigPath, "utf8"));
    return {
      ...mergeMissing(config, DEFAULT_EXTENSION_CONFIG),
      paths: {
        localConfigPath,
        exampleConfigPath,
      },
    };
  } catch {
    return {
      ...DEFAULT_EXTENSION_CONFIG,
      paths: {
        localConfigPath,
        exampleConfigPath,
      },
    };
  }
}

function mergeMissing(value, defaults) {
  const result = Array.isArray(value) ? [...value] : { ...(value || {}) };
  for (const [key, defaultValue] of Object.entries(defaults)) {
    if (result[key] === undefined) {
      result[key] = clone(defaultValue);
    } else if (isPlainObject(result[key]) && isPlainObject(defaultValue)) {
      result[key] = mergeMissing(result[key], defaultValue);
    }
  }
  return result;
}

function clone(value) {
  return value && typeof value === "object" ? JSON.parse(JSON.stringify(value)) : value;
}

function isPlainObject(value) {
  return Boolean(value) && typeof value === "object" && !Array.isArray(value);
}

function redactConfig(config) {
  return {
    ...config,
    apiKey: config?.apiKey ? "[redacted]" : "",
  };
}

module.exports = {
  DEFAULT_EXTENSION_CONFIG,
  ensureExtensionConfigFiles,
  getConfigDirectory,
  getDefaultExtensionConfigText,
  getExampleConfigPath,
  getLocalConfigPath,
  readExtensionConfig,
  redactConfig,
};
