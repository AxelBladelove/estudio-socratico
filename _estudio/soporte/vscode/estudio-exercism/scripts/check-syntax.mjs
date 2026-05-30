import { readdirSync, readFileSync, statSync } from "node:fs";
import { join, relative } from "node:path";
import vm from "node:vm";

const root = process.cwd();
const files = collect(root)
  .filter((file) => file.endsWith(".js"))
  .filter((file) => !file.includes("\\node_modules\\") && !file.includes("/node_modules/"));

for (const file of files) {
  const source = readFileSync(file, "utf8");
  new vm.Script(source, { filename: relative(root, file) });
}

console.log(`Syntax OK: ${files.length} JavaScript files checked.`);

function collect(dir) {
  const entries = [];
  for (const item of readdirSync(dir)) {
    if (item === "node_modules" || item === ".git") continue;
    const full = join(dir, item);
    const stat = statSync(full);
    if (stat.isDirectory()) {
      entries.push(...collect(full));
    } else {
      entries.push(full);
    }
  }
  return entries;
}
