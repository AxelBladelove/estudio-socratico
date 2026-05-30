const fs = require("fs");
const path = require("path");
const { createAiProvider } = require("./providers");
const { selectModel } = require("./modelSelection");

const MAX_FILE_CHARS = 12000;
const MAX_VISIBLE_TEXT_CHARS = 16000;

async function analyzeExercise(root, targetPath, config, options = {}) {
  const exerciseRoot = resolveExerciseRoot(root, targetPath);
  if (!exerciseRoot) {
    throw new Error("No pude detectar un ejercicio de Estudio Socratico desde el archivo activo.");
  }

  const input = buildExerciseAnalysisInput(exerciseRoot);
  const provider = createAiProvider(config, options.providerOptions || {});
  const availability = await provider.isAvailable();
  const modelList = typeof provider.listModels === "function" ? await provider.listModels().catch(() => []) : [];
  const selectedModel = selectModel(
    config.experimental?.preferredFreeModelStrategy,
    modelList,
    provider.name === "gemini" ? config.model : "",
  );

  let result;
  if (availability.available) {
    const completion = await provider.complete(buildAnalysisPrompt(input), { model: selectedModel });
    result = completion.ok
      ? parseProviderResult(completion.text, provider.name, completion.model || selectedModel)
      : unavailableResult(input, provider.name, selectedModel, completion.reason);
  } else {
    result = unavailableResult(input, provider.name, selectedModel, availability.reason);
  }

  result = normalizeAnalysisResult(result, input, config);
  const shouldApply = Boolean(!options.dryRun && result.shouldApplyAutomatically);
  const applied = shouldApply ? applyAnalysisResult(exerciseRoot, input, result, config) : { appliedHeaderPatches: [], appendedInstructions: false };
  return { exerciseRoot, input, result, applied };
}

function buildExerciseAnalysisInput(exerciseRoot) {
  const metaPath = path.join(exerciseRoot, ".estudio-exercism.json");
  const metadata = readJson(metaPath) || {};
  const supportRoot = metadata.supportRoot ? path.join(exerciseRoot, metadata.supportRoot) : path.join(exerciseRoot, ".estudio-exercism", "support");
  const safeSupportRoot = fs.existsSync(supportRoot) ? supportRoot : exerciseRoot;
  const supportFiles = listFilesSafe(safeSupportRoot);
  const visibleFiles = listFilesSafe(exerciseRoot)
    .filter((file) => !isInside(file, safeSupportRoot))
    .filter((file) => !file.includes(`${path.sep}.estudio-exercism${path.sep}`));

  const translatedInstructions = readFirstExisting([
    path.join(safeSupportRoot, "README.md"),
    path.join(exerciseRoot, "README.md"),
  ]);
  const visibleInstructions = visibleFiles
    .filter((file) => file.toLowerCase().endsWith(".c"))
    .map((file) => readFileEntry(exerciseRoot, file))
    .map((entry) => extractLeadingComment(entry.content))
    .filter(Boolean)
    .join("\n\n")
    .slice(0, MAX_VISIBLE_TEXT_CHARS);
  const hiddenHelpMarkdown = supportFiles
    .filter((file) => /(^|[\\/])(help|hints)\.md$/i.test(file))
    .map((file) => readFileEntry(safeSupportRoot, file))
    .map((entry) => `# ${entry.path}\n${entry.content}`)
    .join("\n\n");
  const hiddenTests = supportFiles
    .filter((file) => isTestFile(file))
    .map((file) => readFileEntry(safeSupportRoot, file));
  const existingHeaderFiles = [...visibleFiles, ...supportFiles]
    .filter((file) => file.toLowerCase().endsWith(".h"))
    .map((file) => readFileEntry(isInside(file, safeSupportRoot) ? safeSupportRoot : exerciseRoot, file));
  const existingCFiles = [...visibleFiles, ...supportFiles]
    .filter((file) => file.toLowerCase().endsWith(".c"))
    .filter((file) => !isTestFile(file))
    .map((file) => readFileEntry(isInside(file, safeSupportRoot) ? safeSupportRoot : exerciseRoot, file));
  const configFiles = supportFiles
    .filter((file) => /(^|[\\/])(makefile|config\.json|metadata\.json)$/i.test(file) || /\.(toml|mk)$/i.test(file))
    .map((file) => readFileEntry(safeSupportRoot, file));

  return {
    exerciseSlug: metadata.slug || path.basename(exerciseRoot),
    track: metadata.track || "c",
    visibleInstructions,
    translatedInstructions,
    hiddenHelpMarkdown,
    hiddenTests,
    existingHeaderFiles,
    existingCFiles,
    metadata,
    configFiles,
    detectedFiles: {
      headers: existingHeaderFiles.map((file) => file.path),
      cFiles: existingCFiles.map((file) => file.path),
      tests: hiddenTests.map((file) => file.path),
      config: configFiles.map((file) => file.path),
    },
  };
}

function buildAnalysisPrompt(input) {
  return JSON.stringify({
    role: "Estudio Socratico 2.5 analiza ejercicios de C sin resolverlos.",
    rules: [
      "Devuelve JSON valido solamente.",
      "No escribas solucion en .c.",
      "No copies tests completos.",
      "No copies help.md completo.",
      "Puedes sugerir prototipos minimos para .h.",
      "Puedes proponer un apendice didactico breve para README.md.",
      "No reveles asserts exactos ni entradas completas de tests ocultos.",
    ],
    expectedSchema: {
      headerPatches: [{ path: "archivo.h", action: "create|update", content: "prototipos C minimos", rationale: "breve" }],
      instructionAppendix: "texto didactico breve en espanol",
      warnings: ["advertencias"],
      confidence: 0.0,
      solutionLeakRisk: "low|medium|high",
    },
    input,
  });
}

function parseProviderResult(text, providerUsed, modelUsed) {
  try {
    const parsed = JSON.parse(extractJson(text));
    return {
      headerPatches: Array.isArray(parsed.headerPatches) ? parsed.headerPatches : [],
      instructionAppendix: parsed.instructionAppendix || "",
      warnings: Array.isArray(parsed.warnings) ? parsed.warnings : [],
      providerUsed,
      modelUsed,
      confidence: Number(parsed.confidence || 0),
      solutionLeakRisk: parsed.solutionLeakRisk || "medium",
    };
  } catch {
    return {
      headerPatches: [],
      instructionAppendix: "",
      warnings: ["El proveedor IA no devolvio JSON valido."],
      providerUsed,
      modelUsed,
      confidence: 0,
      solutionLeakRisk: "medium",
    };
  }
}

function normalizeAnalysisResult(result, input, config) {
  const safeHeaderPatches = (result.headerPatches || [])
    .map(normalizeHeaderPatch)
    .filter((patch) => patch && isSafeHeaderPatch(patch));
  const heuristicPrototypes = safeHeaderPatches.length === 0 ? inferPrototypeHints(input) : [];
  const warnings = [...(result.warnings || [])];
  if (heuristicPrototypes.length > 0) {
    warnings.push("Sin parche IA seguro; se muestran candidatos inferidos para revision humana.");
  }

  return {
    headerPatches: safeHeaderPatches,
    prototypeHints: safeHeaderPatches.length > 0 ? extractPrototypeHints(safeHeaderPatches) : heuristicPrototypes,
    instructionAppendix: sanitizeVisibleText(result.instructionAppendix || ""),
    warnings: warnings.map(sanitizeVisibleText).filter(Boolean),
    providerUsed: result.providerUsed || "none",
    modelUsed: result.modelUsed || "",
    confidence: clamp(Number(result.confidence || 0), 0, 1),
    shouldApplyAutomatically: Boolean(
      config.experimental?.applyHeaderPatchesAutomatically ||
      config.experimental?.appendInstructionHintsAutomatically,
    ) && String(result.solutionLeakRisk || "medium").toLowerCase() === "low",
    solutionLeakRisk: result.solutionLeakRisk || "medium",
  };
}

function applyAnalysisResult(exerciseRoot, input, result, config) {
  const appliedHeaderPatches = [];
  if (config.experimental?.applyHeaderPatchesAutomatically) {
    for (const patch of result.headerPatches) {
      const target = path.resolve(exerciseRoot, patch.path);
      if (!target.startsWith(path.resolve(exerciseRoot)) || !target.toLowerCase().endsWith(".h")) continue;
      fs.mkdirSync(path.dirname(target), { recursive: true });
      fs.writeFileSync(target, patch.content.trimEnd() + "\n", "utf8");
      appliedHeaderPatches.push(path.relative(exerciseRoot, target));
    }
  }

  let appendedInstructions = false;
  if (config.experimental?.appendInstructionHintsAutomatically && result.instructionAppendix) {
    const supportRoot = input.metadata.supportRoot ? path.join(exerciseRoot, input.metadata.supportRoot) : path.join(exerciseRoot, ".estudio-exercism", "support");
    const readmePath = fs.existsSync(path.join(supportRoot, "README.md")) ? path.join(supportRoot, "README.md") : path.join(exerciseRoot, "README.md");
    if (fs.existsSync(readmePath)) {
      const marker = "\n\n## Apoyo didactico de Estudio 2.5\n\n";
      const current = fs.readFileSync(readmePath, "utf8");
      if (!current.includes(marker.trim())) {
        fs.writeFileSync(readmePath, `${current.trimEnd()}${marker}${result.instructionAppendix.trim()}\n`, "utf8");
        appendedInstructions = true;
      }
    }
  }

  return { appliedHeaderPatches, appendedInstructions };
}

function formatAnalysisDryRun(analysis) {
  const { input, result, applied } = analysis;
  return [
    "Estudio Socratico 2.5 - Analisis IA experimental",
    "",
    `Ejercicio: ${input.exerciseSlug}`,
    `Provider: ${result.providerUsed}`,
    `Modelo: ${result.modelUsed || "no disponible"}`,
    `Confianza: ${Math.round(result.confidence * 100)}%`,
    `Riesgo de revelar solucion: ${result.solutionLeakRisk}`,
    "",
    "Archivos detectados:",
    `- .h: ${input.detectedFiles.headers.length ? input.detectedFiles.headers.join(", ") : "ninguno"}`,
    `- .c: ${input.detectedFiles.cFiles.length ? input.detectedFiles.cFiles.join(", ") : "ninguno"}`,
    `- tests ocultos usados como contexto: ${input.detectedFiles.tests.length}`,
    `- config/makefile: ${input.detectedFiles.config.length ? input.detectedFiles.config.join(", ") : "ninguno"}`,
    "",
    "Prototipos sugeridos:",
    ...(result.prototypeHints.length ? result.prototypeHints.map((line) => `- ${line}`) : ["- ninguno"]),
    "",
    "Hints didacticos sugeridos:",
    result.instructionAppendix ? result.instructionAppendix : "Sin apendice sugerido.",
    "",
    "Advertencias:",
    ...(result.warnings.length ? result.warnings.map((line) => `- ${line}`) : ["- ninguna"]),
    "",
    "Cambios aplicados:",
    `- Headers: ${applied.appliedHeaderPatches.length ? applied.appliedHeaderPatches.join(", ") : "ninguno"}`,
    `- README: ${applied.appendedInstructions ? "apendice agregado" : "sin cambios"}`,
    "",
    "Nota: los tests y help.md fueron analizados como contexto oculto; no se muestran completos aqui.",
  ].join("\n");
}

function unavailableResult(input, providerUsed, modelUsed, reason) {
  return {
    headerPatches: [],
    prototypeHints: inferPrototypeHints(input),
    instructionAppendix: "",
    warnings: [`Proveedor no disponible: ${reason || "sin detalle"}`],
    providerUsed,
    modelUsed,
    confidence: 0,
    shouldApplyAutomatically: false,
    solutionLeakRisk: "low",
  };
}

function resolveExerciseRoot(root, targetPath) {
  let current = fs.existsSync(targetPath) && fs.statSync(targetPath).isDirectory() ? targetPath : path.dirname(targetPath);
  const repoRoot = path.resolve(root);
  while (current && path.resolve(current).startsWith(repoRoot)) {
    if (fs.existsSync(path.join(current, ".estudio-exercism.json"))) return current;
    const parent = path.dirname(current);
    if (parent === current) break;
    current = parent;
  }
  return null;
}

function readJson(file) {
  try {
    return JSON.parse(fs.readFileSync(file, "utf8"));
  } catch {
    return null;
  }
}

function listFilesSafe(dir) {
  if (!fs.existsSync(dir)) return [];
  const files = [];
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    if (entry.name === "node_modules" || entry.name === ".git") continue;
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      files.push(...listFilesSafe(full));
    } else {
      files.push(full);
    }
  }
  return files;
}

function readFileEntry(base, file) {
  return {
    path: path.relative(base, file).replace(/\\/g, "/"),
    content: fs.readFileSync(file, "utf8").slice(0, MAX_FILE_CHARS),
  };
}

function readFirstExisting(files) {
  for (const file of files) {
    if (fs.existsSync(file)) return fs.readFileSync(file, "utf8").slice(0, MAX_VISIBLE_TEXT_CHARS);
  }
  return "";
}

function isInside(file, dir) {
  const relative = path.relative(dir, file);
  return relative && !relative.startsWith("..") && !path.isAbsolute(relative);
}

function isTestFile(file) {
  const normalized = file.toLowerCase();
  return normalized.endsWith(".c") && /(^|[\\/])(test|tests|test_|\w+_test)/i.test(normalized);
}

function extractLeadingComment(source) {
  const match = String(source || "").match(/^\s*\/\*([\s\S]*?)\*\//);
  return match ? match[1].trim().slice(0, MAX_VISIBLE_TEXT_CHARS) : "";
}

function extractJson(text) {
  const value = String(text || "").trim();
  const start = value.indexOf("{");
  const end = value.lastIndexOf("}");
  return start >= 0 && end > start ? value.slice(start, end + 1) : value;
}

function normalizeHeaderPatch(patch) {
  if (!patch || typeof patch !== "object") return null;
  return {
    path: String(patch.path || "").replace(/\\/g, "/"),
    action: String(patch.action || "update"),
    content: sanitizeHeaderContent(String(patch.content || "")),
    rationale: sanitizeVisibleText(patch.rationale || ""),
  };
}

function sanitizeHeaderContent(content) {
  return content
    .split(/\r?\n/)
    .filter((line) => !/\bassert\s*\(|TEST\s*\(|RUN_TEST\b/i.test(line))
    .join("\n")
    .trim();
}

function isSafeHeaderPatch(patch) {
  if (!patch.path || !patch.path.toLowerCase().endsWith(".h")) return false;
  if (patch.path.includes("..")) return false;
  if (!patch.content || patch.content.includes("{") || patch.content.includes("}")) return false;
  return patch.content.split(/\r?\n/).some((line) => /\)\s*;/.test(line) || /^#ifndef\b|^#define\b|^#endif\b/.test(line.trim()));
}

function extractPrototypeHints(patches) {
  return patches
    .flatMap((patch) => patch.content.split(/\r?\n/))
    .map((line) => line.trim())
    .filter((line) => /\)\s*;/.test(line))
    .slice(0, 12);
}

function inferPrototypeHints(input) {
  const candidates = new Map();
  const known = new Set(["assert", "strcmp", "strlen", "printf", "malloc", "free", "test", "main", "exit"]);
  for (const file of input.hiddenTests || []) {
    const code = stripCCommentsAndStrings(file.content || "");
    for (const match of code.matchAll(/\b([A-Za-z_][A-Za-z0-9_]*)\s*\(/g)) {
      const name = match[1];
      if (known.has(name) || name.startsWith("UNITY_")) continue;
      candidates.set(name, (candidates.get(name) || 0) + 1);
    }
  }
  return [...candidates.entries()]
    .sort((a, b) => b[1] - a[1])
    .slice(0, 8)
    .map(([name]) => `${name}(...)`);
}

function stripCCommentsAndStrings(source) {
  return String(source || "")
    .replace(/\/\*[\s\S]*?\*\//g, " ")
    .replace(/\/\/.*$/gm, " ")
    .replace(/"(?:\\.|[^"\\])*"/g, '""')
    .replace(/'(?:\\.|[^'\\])*'/g, "''");
}

function sanitizeVisibleText(value) {
  return String(value || "")
    .replace(/```[\s\S]*?```/g, "[bloque de codigo omitido]")
    .replace(/\b(assert|TEST|RUN_TEST)\s*\([^)]*\);?/gi, "[detalle de test oculto]")
    .replace(/\s{3,}/g, " ")
    .trim()
    .slice(0, 3000);
}

function clamp(value, min, max) {
  return Math.max(min, Math.min(max, Number.isFinite(value) ? value : min));
}

module.exports = {
  analyzeExercise,
  buildAnalysisPrompt,
  buildExerciseAnalysisInput,
  formatAnalysisDryRun,
  inferPrototypeHints,
  resolveExerciseRoot,
};
