import { cpSync, existsSync, mkdirSync, rmSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const uiRoot = resolve(here, "..");
const source = resolve(uiRoot, "dist");
const target = resolve(uiRoot, "..", "src", "EstudioSocratico.Configurator.App", "wwwroot");

if (!existsSync(source)) {
  throw new Error(`Vite output not found: ${source}`);
}

rmSync(target, { recursive: true, force: true });
mkdirSync(target, { recursive: true });
cpSync(source, target, { recursive: true });

console.log(`Copied UI build to ${target}`);
