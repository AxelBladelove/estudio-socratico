import { spawnSync } from "node:child_process";
import { existsSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const extensionRoot = resolve(here, "..");
const repoRoot = resolve(extensionRoot, "..", "..", "..", "..");
const buildScript = join(repoRoot, "_estudio", "soporte", "engine", "build-engine.cmd");
const enginePath = join(extensionRoot, "engine", "estudio-engine.exe");

if (!existsSync(buildScript)) {
  throw new Error(`No se encontro ${buildScript}`);
}

const result = spawnSync(buildScript, {
  cwd: repoRoot,
  shell: true,
  stdio: "inherit",
});

if (result.status !== 0) {
  process.exit(result.status ?? 1);
}

if (!existsSync(enginePath)) {
  throw new Error(`No se genero ${enginePath}`);
}
