import { mkdtempSync, mkdirSync, readFileSync, rmSync, writeFileSync, existsSync, readdirSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { createRequire } from "node:module";

const require = createRequire(import.meta.url);
const repoRoot = process.cwd();
const checks = [];

await check("Bun lock versionado", () => assert(existsSync(join(repoRoot, "bun.lock")), "falta bun.lock"));
await check("packageManager usa Bun", () => {
  for (const file of [
    "package.json",
    "_estudio/installer/ui/package.json",
    "_estudio/soporte/vscode/estudio-exercism/package.json",
  ]) {
    const json = readJson(join(repoRoot, file));
    assert(String(json.packageManager || "").startsWith("bun@"), `${file} no declara Bun`);
  }
});
await check("Workflows usan setup-bun y bun ci", () => {
  const workflows = listFiles(join(repoRoot, ".github", "workflows")).filter((file) => /\.(ya?ml)$/i.test(file));
  const text = workflows.map((file) => readFileSync(file, "utf8")).join("\n");
  assert(text.includes("oven-sh/setup-bun"), "no se encontro setup-bun");
  assert(text.includes("bun ci"), "no se encontro bun ci");
  assert(!/\bnpm\s+(ci|install)\b/i.test(text), "workflow conserva npm ci/install sin fallback documentado");
});
await check("No quedan package-lock.json rastreados en el workspace", () => {
  const locks = listFiles(repoRoot).filter((file) => file.endsWith("package-lock.json"));
  assert(locks.length === 0, `package-lock encontrados: ${locks.join(", ")}`);
});
await check("Config example 2.5 con flags apagados", () => {
  const config = readJson(join(repoRoot, "usuario/config/estudio-socratico.extension.example.json"));
  assert(config.model === "gemini-2.5-flash", "modelo default inesperado");
  for (const key of ["aiExerciseAnalysis", "opencodeIntegration", "aiGeneratedTests", "autoGitSync"]) {
    assert(config.features?.[key] === false, `${key} debe iniciar apagado`);
  }
  assert(config.experimental?.aiExerciseAnalysisProvider === "gemini", "provider experimental default debe ser gemini");
});
await check("Config local se completa sin borrar apiKey", () => {
  const { readExtensionConfig } = require(join(repoRoot, "_estudio/soporte/vscode/estudio-exercism/src/config.js"));
  const root = mkdtempSync(join(tmpdir(), "estudio-v25-config-"));
  mkdirSync(join(root, "usuario", "config"), { recursive: true });
  const local = join(root, "usuario", "config", "estudio-socratico.extension.local.json");
  writeFileSync(local, JSON.stringify({ apiKey: "keep-me" }, null, 2), "utf8");
  const config = readExtensionConfig(root);
  const updated = readFileSync(local, "utf8");
  rmSync(root, { recursive: true, force: true });
  assert(updated.includes("keep-me"), "apiKey fue borrada");
  assert(config.features.aiExerciseAnalysis === false, "aiExerciseAnalysis debe completar en false");
  assert(config.features.opencodeIntegration === false, "opencodeIntegration debe completar en false");
});
await check("GeminiProvider usa model y apiKey local antes que env", async () => {
  const { GeminiProvider } = require(join(repoRoot, "_estudio/soporte/vscode/estudio-exercism/src/ai/geminiProvider.js"));
  process.env.GEMINI_API_KEY = "env-key";
  let capturedUrl = "";
  const provider = new GeminiProvider({ provider: "gemini", apiKey: "local-key", model: "gemini-test" }, async (url) => {
    capturedUrl = String(url);
    return {
      ok: true,
      text: async () => JSON.stringify({ candidates: [{ content: { parts: [{ text: "{}" }] } }] }),
    };
  });
  const result = await provider.complete("{}", {});
  assert(result.ok, "Gemini fake call fallo");
  assert(capturedUrl.includes("gemini-test"), "no uso model del JSON");
  assert(capturedUrl.includes("local-key"), "no uso apiKey local");
  assert(!capturedUrl.includes("env-key"), "uso env antes que apiKey local");
});
await check("OpenCode no se ejecuta si el flag esta apagado", async () => {
  const { OpenCodeProvider } = require(join(repoRoot, "_estudio/soporte/vscode/estudio-exercism/src/ai/opencodeProvider.js"));
  let calls = 0;
  const provider = new OpenCodeProvider({ features: { opencodeIntegration: false }, experimental: { opencodeCommand: "opencode" } }, () => {
    calls += 1;
  });
  const state = await provider.isAvailable();
  assert(!state.available, "OpenCode no debe estar available con flag apagado");
  assert(calls === 0, "OpenCode fue ejecutado con flag apagado");
});
await check("Exercise analysis no muestra tests completos ni aplica con flags false", async () => {
  const { analyzeExercise, formatAnalysisDryRun } = require(join(repoRoot, "_estudio/soporte/vscode/estudio-exercism/src/ai/exerciseAnalysis.js"));
  const root = mkdtempSync(join(tmpdir(), "estudio-v25-analysis-"));
  const exercise = join(root, "Ejercicios", "Resistor Color");
  const support = join(exercise, ".estudio-exercism", "support");
  mkdirSync(support, { recursive: true });
  writeFileSync(join(exercise, ".estudio-exercism.json"), JSON.stringify({ slug: "resistor-color", track: "c", supportRoot: ".estudio-exercism/support" }), "utf8");
  writeFileSync(join(exercise, "resistor_color.c"), "/* instrucciones visibles */\n", "utf8");
  writeFileSync(join(support, "README.md"), "# Resistor Color", "utf8");
  writeFileSync(join(support, "test_resistor_color.c"), "void test_black(void){ assert(color_code(\"black\") == 0); }", "utf8");
  const analysis = await analyzeExercise(root, exercise, {
    provider: "gemini",
    model: "gemini-2.5-flash",
    features: { aiExerciseAnalysis: true, opencodeIntegration: false },
    experimental: { applyHeaderPatchesAutomatically: false, appendInstructionHintsAutomatically: false },
  }, { dryRun: false });
  const output = formatAnalysisDryRun(analysis);
  rmSync(root, { recursive: true, force: true });
  assert(!output.includes("assert(color_code"), "salida visible filtro tests incompleto");
  assert(analysis.applied.appliedHeaderPatches.length === 0, "aplico headers con flags false");
  assert(analysis.applied.appendedInstructions === false, "modifico README con flags false");
});
await check("Comandos VS Code experimentales y existentes", () => {
  const pkg = readJson(join(repoRoot, "_estudio/soporte/vscode/estudio-exercism/package.json"));
  const commands = new Set(pkg.contributes.commands.map((command) => command.command));
  for (const command of [
    "estudioExercism.openPanel",
    "estudioExercism.testCurrent",
    "estudioExercism.submitCurrent",
    "estudioExercism.validateCurrent",
    "estudioExercism.analyzeExerciseWithAi",
    "estudioExercism.generateAiLogicTests",
  ]) {
    assert(commands.has(command), `falta comando ${command}`);
  }
  assert(existsSync(join(repoRoot, "_estudio/soporte/scripts/compilar_y_grabar.bat")), "F9 script faltante");
});

const failed = checks.filter((item) => !item.ok);
for (const item of checks) {
  console.log(`${item.ok ? "OK" : "FAIL"} ${item.name}${item.error ? `: ${item.error}` : ""}`);
}
if (failed.length > 0) {
  process.exitCode = 1;
}

async function check(name, fn) {
  try {
    await fn();
    checks.push({ name, ok: true });
  } catch (error) {
    checks.push({ name, ok: false, error: error.message });
  }
}

function readJson(file) {
  return JSON.parse(readFileSync(file, "utf8"));
}

function listFiles(dir) {
  const results = [];
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    if (entry.name === ".git" || entry.name === "node_modules" || entry.name === "bin" || entry.name === "obj" || entry.name === "dist") {
      continue;
    }
    const full = join(dir, entry.name);
    if (entry.isDirectory()) {
      results.push(...listFiles(full));
    } else {
      results.push(full);
    }
  }
  return results;
}

function assert(condition, message) {
  if (!condition) throw new Error(message);
}
