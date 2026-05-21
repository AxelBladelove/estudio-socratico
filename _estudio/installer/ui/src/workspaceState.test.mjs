import assert from "node:assert/strict";
import {
  normalizeAliasInput,
  recommendedWorkspacePath,
  resolveWorkspaceSelection,
} from "./workspaceState.js";

{
  const actual = recommendedWorkspacePath("C:\\Users\\axelb\\Estudio-Socratico-axel", "Ana Maria");
  assert.equal(actual, "C:\\Users\\axelb\\Estudio-Socratico-ana-maria");
}

{
  const first = recommendedWorkspacePath("C:\\Users\\axelb\\Estudio-Socratico-axel", "axel");
  const second = recommendedWorkspacePath(first, "ana maria");
  assert.equal(second, "C:\\Users\\axelb\\Estudio-Socratico-ana-maria");
}

{
  const actual = resolveWorkspaceSelection({
    currentPath: "D:\\Cursos\\mi-workspace",
    manual: true,
    alias: normalizeAliasInput("otro"),
    referencePath: "C:\\Users\\axelb\\Estudio-Socratico-axel",
  });
  assert.equal(actual, "D:\\Cursos\\mi-workspace");
}

console.log("workspaceState tests passed");
