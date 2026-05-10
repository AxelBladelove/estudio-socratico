import { createServer } from "node:http";
import { spawn } from "node:child_process";
import { existsSync } from "node:fs";
import fs from "node:fs/promises";
import path from "node:path";
import process from "node:process";

const DRIVE_SCOPE = "https://www.googleapis.com/auth/drive.file";
const TOKEN_URL = "https://oauth2.googleapis.com/token";
const AUTH_URL = "https://accounts.google.com/o/oauth2/v2/auth";
const DRIVE_API = "https://www.googleapis.com/drive/v3";
const DRIVE_UPLOAD = "https://www.googleapis.com/upload/drive/v3";
const REDIRECT_PORT = 53682;
const REDIRECT_PATH = "/oauth2callback";

const repoRoot = findRepoRoot(process.cwd());
const localRoot = path.join(repoRoot, ".estudio-drive");
const tokenPath = path.join(localRoot, "token.json");
const oauthClientPath = path.join(localRoot, "oauth-client.json");
const sourceRoot = path.join(localRoot, "source");
const generatedRoot = path.join(localRoot, "generated");
const configPath = path.join(repoRoot, "soporte", "drive", "drive.config.json");
const catalogRoot = path.join(repoRoot, "soporte", "exercism", "catalogs");

main().catch((error) => {
  console.error(`[ERROR] ${error.message}`);
  process.exitCode = 1;
});

async function main() {
  const { command, options } = parseArgs(process.argv.slice(2));
  if (command === "help" || !command) {
    printHelp();
    return;
  }

  if (command === "auth") {
    await authenticate();
    return;
  }

  if (command === "check") {
    const auth = await getAuthorizedClient();
    const config = await readJson(configPath);
    console.log(`[OK] OAuth local listo. Root Drive: ${config.rootFolderId}`);
    for (const [provider, settings] of Object.entries(config.providers || {})) {
      const files = await listDriveFiles(auth, settings.folderId, 10);
      console.log(`[OK] ${provider}: ${files.length} archivo(s) visibles en ${settings.folderId}`);
    }
    return;
  }

  if (command === "generate") {
    const result = await generateAll(options);
    console.log(`[OK] Generados ${result.generated} archivo(s) markdown en ${generatedRoot}`);
    if (result.missing > 0) {
      console.log(`[AVISO] ${result.missing} ejercicio(s) no tienen enunciado local para subir.`);
    }
    return;
  }

  if (command === "sync") {
    await syncAll(options);
    return;
  }

  throw new Error(`Comando no reconocido: ${command}`);
}

function parseArgs(args) {
  const command = args[0] || "help";
  const options = {
    provider: null,
    dryRun: false,
    keepLocalText: false,
    allowFallback: false,
  };

  for (let i = 1; i < args.length; i++) {
    const arg = args[i];
    if (arg === "--provider") {
      options.provider = args[++i] || null;
    } else if (arg === "--dry-run") {
      options.dryRun = true;
    } else if (arg === "--keep-local-text") {
      options.keepLocalText = true;
    } else if (arg === "--allow-fallback") {
      options.allowFallback = true;
    } else {
      throw new Error(`Argumento no reconocido: ${arg}`);
    }
  }

  return { command, options };
}

function printHelp() {
  console.log(`Estudio Socratico Drive Sync

Uso:
  npm run drive:auth
  npm run drive:check
  npm run drive:generate -- [--provider w3schools]
  npm run drive:sync -- [--provider w3schools] [--dry-run] [--keep-local-text]

Credenciales locales:
  .estudio-drive/oauth-client.json
  .estudio-drive/token.json

Notas:
  - Estos comandos son solo para mantenedores.
  - Los estudiantes descargan por driveFileId publico; no usan OAuth.
  - drive:sync elimina instructionMarkdown del catalogo despues de subir,
    salvo que uses --keep-local-text.`);
}

function findRepoRoot(start) {
  let current = path.resolve(start);
  while (true) {
    if (existsSync(path.join(current, "package.json")) && existsSync(path.join(current, "soporte"))) {
      return current;
    }
    const parent = path.dirname(current);
    if (parent === current) {
      throw new Error("No encontre la raiz del repo.");
    }
    current = parent;
  }
}

async function readJson(filePath) {
  const text = await fs.readFile(filePath, "utf8");
  return JSON.parse(text.replace(/^\uFEFF/, ""));
}

async function writeJson(filePath, value) {
  await fs.mkdir(path.dirname(filePath), { recursive: true });
  await fs.writeFile(filePath, `${JSON.stringify(value, null, 2)}\n`, "utf8");
}

async function authenticate() {
  const client = await readOAuthClient();
  await fs.mkdir(localRoot, { recursive: true });

  const redirectUri = getRedirectUri(client);
  const state = `${Date.now()}-${Math.random().toString(16).slice(2)}`;
  const url = new URL(AUTH_URL);
  url.searchParams.set("client_id", client.client_id);
  url.searchParams.set("redirect_uri", redirectUri);
  url.searchParams.set("response_type", "code");
  url.searchParams.set("scope", DRIVE_SCOPE);
  url.searchParams.set("access_type", "offline");
  url.searchParams.set("prompt", "consent");
  url.searchParams.set("state", state);

  const codePromise = waitForOAuthCode(state);
  console.log("[INFO] Abriendo navegador para autorizar Google Drive...");
  console.log(String(url));
  openBrowser(String(url));
  const code = await codePromise;
  const token = await exchangeCodeForToken(client, redirectUri, code);
  token.expires_at = Date.now() + (Number(token.expires_in || 3600) * 1000);
  await writeJson(tokenPath, token);
  console.log(`[OK] Token local guardado en ${tokenPath}`);
}

async function readOAuthClient() {
  if (!existsSync(oauthClientPath)) {
    throw new Error(
      `Falta ${oauthClientPath}. Crea un OAuth Client tipo Desktop en Google Cloud y guarda ahi el JSON descargado.`,
    );
  }

  const raw = await readJson(oauthClientPath);
  const client = raw.installed || raw.web || raw;
  if (!client.client_id || !client.client_secret) {
    throw new Error("oauth-client.json no contiene client_id y client_secret.");
  }
  return client;
}

function getRedirectUri(client) {
  const preferred = `http://127.0.0.1:${REDIRECT_PORT}${REDIRECT_PATH}`;
  const allowed = client.redirect_uris || [];
  return allowed.includes(preferred) ? preferred : preferred;
}

function waitForOAuthCode(expectedState) {
  return new Promise((resolve, reject) => {
    const server = createServer((request, response) => {
      const requestUrl = new URL(request.url || "/", `http://127.0.0.1:${REDIRECT_PORT}`);
      if (requestUrl.pathname !== REDIRECT_PATH) {
        response.writeHead(404);
        response.end("Not found");
        return;
      }

      const error = requestUrl.searchParams.get("error");
      const state = requestUrl.searchParams.get("state");
      const code = requestUrl.searchParams.get("code");
      if (error) {
        response.end("Autorizacion cancelada. Puedes cerrar esta pestana.");
        server.close();
        reject(new Error(`Google devolvio error OAuth: ${error}`));
        return;
      }
      if (state !== expectedState || !code) {
        response.end("Respuesta OAuth invalida. Puedes cerrar esta pestana.");
        server.close();
        reject(new Error("Respuesta OAuth invalida."));
        return;
      }

      response.end("Google Drive conectado. Puedes cerrar esta pestana y volver a la terminal.");
      server.close();
      resolve(code);
    });

    server.on("error", reject);
    server.listen(REDIRECT_PORT, "127.0.0.1");
  });
}

function openBrowser(url) {
  if (process.platform === "win32") {
    spawn("cmd", ["/c", "start", "", url], { detached: true, stdio: "ignore" }).unref();
  } else if (process.platform === "darwin") {
    spawn("open", [url], { detached: true, stdio: "ignore" }).unref();
  } else {
    spawn("xdg-open", [url], { detached: true, stdio: "ignore" }).unref();
  }
}

async function exchangeCodeForToken(client, redirectUri, code) {
  const body = new URLSearchParams({
    client_id: client.client_id,
    client_secret: client.client_secret,
    code,
    redirect_uri: redirectUri,
    grant_type: "authorization_code",
  });
  return tokenRequest(body);
}

async function refreshAccessToken(client, refreshToken) {
  const body = new URLSearchParams({
    client_id: client.client_id,
    client_secret: client.client_secret,
    refresh_token: refreshToken,
    grant_type: "refresh_token",
  });
  return tokenRequest(body);
}

async function tokenRequest(body) {
  const response = await fetch(TOKEN_URL, {
    method: "POST",
    headers: { "content-type": "application/x-www-form-urlencoded" },
    body,
  });
  const text = await response.text();
  const json = text ? JSON.parse(text) : {};
  if (!response.ok) {
    throw new Error(`OAuth fallo (${response.status}): ${json.error_description || json.error || text}`);
  }
  return json;
}

async function getAuthorizedClient() {
  const client = await readOAuthClient();
  if (!existsSync(tokenPath)) {
    throw new Error("No hay token local de Drive. Ejecuta npm run drive:auth.");
  }

  let token = await readJson(tokenPath);
  if (!token.access_token || Date.now() > Number(token.expires_at || 0) - 60000) {
    if (!token.refresh_token) {
      throw new Error("El token local no tiene refresh_token. Ejecuta npm run drive:auth otra vez.");
    }
    const refreshed = await refreshAccessToken(client, token.refresh_token);
    token = {
      ...token,
      ...refreshed,
      refresh_token: refreshed.refresh_token || token.refresh_token,
      expires_at: Date.now() + (Number(refreshed.expires_in || 3600) * 1000),
    };
    await writeJson(tokenPath, token);
  }

  return {
    async request(url, init = {}, retry = true) {
      const response = await fetch(url, {
        ...init,
        headers: {
          ...(init.headers || {}),
          authorization: `Bearer ${token.access_token}`,
        },
      });

      if (response.status === 401 && retry && token.refresh_token) {
        const refreshed = await refreshAccessToken(client, token.refresh_token);
        token = {
          ...token,
          ...refreshed,
          refresh_token: refreshed.refresh_token || token.refresh_token,
          expires_at: Date.now() + (Number(refreshed.expires_in || 3600) * 1000),
        };
        await writeJson(tokenPath, token);
        return this.request(url, init, false);
      }

      return response;
    },
  };
}

async function driveJson(auth, url, init = {}) {
  const response = await auth.request(url, {
    ...init,
    headers: {
      accept: "application/json",
      ...(init.body ? { "content-type": "application/json" } : {}),
      ...(init.headers || {}),
    },
  });
  const text = await response.text();
  const json = text ? JSON.parse(text) : {};
  if (!response.ok) {
    throw new Error(`Drive API fallo (${response.status}): ${json.error?.message || text}`);
  }
  return json;
}

async function listDriveFiles(auth, folderId, pageSize = 100) {
  const query = `'${folderId}' in parents and trashed = false`;
  const url = new URL(`${DRIVE_API}/files`);
  url.searchParams.set("q", query);
  url.searchParams.set("pageSize", String(pageSize));
  url.searchParams.set("fields", "files(id,name,mimeType,webViewLink,modifiedTime)");
  const json = await driveJson(auth, String(url));
  return json.files || [];
}

async function findDriveFileByName(auth, folderId, name) {
  const query = `'${folderId}' in parents and name = '${escapeDriveQuery(name)}' and trashed = false`;
  const url = new URL(`${DRIVE_API}/files`);
  url.searchParams.set("q", query);
  url.searchParams.set("pageSize", "10");
  url.searchParams.set("fields", "files(id,name,webViewLink,modifiedTime)");
  const json = await driveJson(auth, String(url));
  return (json.files || [])[0] || null;
}

function escapeDriveQuery(value) {
  return String(value).replace(/\\/g, "\\\\").replace(/'/g, "\\'");
}

async function uploadMarkdown(auth, { fileId, folderId, name, markdown }) {
  const boundary = `estudio_${Date.now()}_${Math.random().toString(16).slice(2)}`;
  const metadata = fileId ? { name, mimeType: "text/markdown" } : { name, mimeType: "text/markdown", parents: [folderId] };
  const body = [
    `--${boundary}`,
    "Content-Type: application/json; charset=UTF-8",
    "",
    JSON.stringify(metadata),
    `--${boundary}`,
    "Content-Type: text/markdown; charset=UTF-8",
    "",
    markdown,
    `--${boundary}--`,
    "",
  ].join("\r\n");

  const endpoint = fileId
    ? `${DRIVE_UPLOAD}/files/${encodeURIComponent(fileId)}?uploadType=multipart&fields=id,name,webViewLink,webContentLink,modifiedTime`
    : `${DRIVE_UPLOAD}/files?uploadType=multipart&fields=id,name,webViewLink,webContentLink,modifiedTime`;
  const response = await auth.request(endpoint, {
    method: fileId ? "PATCH" : "POST",
    headers: { "content-type": `multipart/related; boundary=${boundary}` },
    body,
  });
  const text = await response.text();
  const json = text ? JSON.parse(text) : {};
  if (!response.ok) {
    throw new Error(`Drive upload fallo (${response.status}): ${json.error?.message || text}`);
  }
  return json;
}

async function makePublic(auth, fileId) {
  try {
    await driveJson(auth, `${DRIVE_API}/files/${encodeURIComponent(fileId)}/permissions?fields=id`, {
      method: "POST",
      body: JSON.stringify({ type: "anyone", role: "reader" }),
    });
  } catch (error) {
    if (!/already exists|permission/i.test(error.message)) {
      throw error;
    }
  }
}

async function generateAll(options) {
  const config = await readJson(configPath);
  const providers = selectProviders(config, options.provider);
  let generated = 0;
  let missing = 0;

  for (const [provider, settings] of providers) {
    const catalogPath = path.join(catalogRoot, `${settings.catalog || provider}.json`);
    const catalog = await readJson(catalogPath);
    const exercises = catalog.exercises || [];
    for (let index = 0; index < exercises.length; index++) {
      const exercise = exercises[index];
      const source = await resolveMarkdownSource(provider, exercise, options);
      if (!source.markdown) {
        missing++;
        continue;
      }
      const fileName = driveMarkdownFileName(index, exercise);
      const output = path.join(generatedRoot, provider, fileName);
      await fs.mkdir(path.dirname(output), { recursive: true });
      await fs.writeFile(output, withSourceFooter(source.markdown, provider, exercise), "utf8");
      generated++;
    }
  }

  return { generated, missing };
}

async function syncAll(options) {
  const config = await readJson(configPath);
  const auth = options.dryRun ? null : await getAuthorizedClient();
  const providers = selectProviders(config, options.provider);
  let uploaded = 0;
  let skipped = 0;

  for (const [provider, settings] of providers) {
    const catalogPath = path.join(catalogRoot, `${settings.catalog || provider}.json`);
    const catalog = await readJson(catalogPath);
    catalog.sourceFolderUrl = settings.folderUrl || catalog.sourceFolderUrl;
    const exercises = catalog.exercises || [];

    console.log(`[INFO] Sincronizando ${provider} -> ${settings.folderId}`);
    for (let index = 0; index < exercises.length; index++) {
      const exercise = exercises[index];
      const source = await resolveMarkdownSource(provider, exercise, options);
      if (!source.markdown) {
        console.log(`[AVISO] Sin enunciado local para ${provider}:${exercise.slug}; omitido.`);
        skipped++;
        continue;
      }

      const fileName = driveMarkdownFileName(index, exercise);
      const markdown = withSourceFooter(source.markdown, provider, exercise);
      const sourceCopy = path.join(sourceRoot, provider, fileName);
      await fs.mkdir(path.dirname(sourceCopy), { recursive: true });
      await fs.writeFile(sourceCopy, markdown, "utf8");

      if (options.dryRun) {
        console.log(`[DRY] Subiria ${fileName}`);
        continue;
      }

      let fileId = exercise.driveFileId || "";
      if (!fileId) {
        const existing = await findDriveFileByName(auth, settings.folderId, fileName);
        fileId = existing?.id || "";
      }

      const uploadedFile = await uploadMarkdown(auth, {
        fileId,
        folderId: settings.folderId,
        name: fileName,
        markdown,
      });
      await makePublic(auth, uploadedFile.id);

      exercise.driveFileId = uploadedFile.id;
      exercise.driveFileName = fileName;
      if (!options.keepLocalText) {
        delete exercise.instructionMarkdown;
      }
      uploaded++;
      console.log(`[OK] ${provider}:${exercise.slug} -> ${uploadedFile.id}`);
    }

    if (!options.dryRun) {
      await writeJson(catalogPath, catalog);
    }
  }

  console.log(`[OK] Sync terminado. Subidos/actualizados: ${uploaded}. Omitidos: ${skipped}.`);
  if (!options.keepLocalText && !options.dryRun) {
    console.log("[OK] Los instructionMarkdown subidos fueron removidos del catalogo versionado.");
  }
}

function selectProviders(config, providerName) {
  const entries = Object.entries(config.providers || {});
  if (!providerName) return entries;
  const filtered = entries.filter(([provider]) => provider === providerName);
  if (filtered.length === 0) {
    throw new Error(`Proveedor no configurado en Drive: ${providerName}`);
  }
  return filtered;
}

async function resolveMarkdownSource(provider, exercise, options) {
  const candidates = [
    path.join(sourceRoot, provider, `${exercise.slug}.md`),
    path.join(sourceRoot, provider, `${driveMarkdownBaseName(exercise)}.md`),
  ];
  for (const candidate of candidates) {
    if (existsSync(candidate)) {
      return { markdown: await fs.readFile(candidate, "utf8"), origin: candidate };
    }
  }

  if (exercise.instructionMarkdown) {
    return { markdown: String(exercise.instructionMarkdown), origin: "catalog" };
  }

  if (options.allowFallback) {
    return { markdown: fallbackMarkdown(provider, exercise), origin: "fallback" };
  }

  return { markdown: "", origin: "missing" };
}

function driveMarkdownFileName(index, exercise) {
  const prefix = String(index + 1).padStart(3, "0");
  return `${prefix}-${driveMarkdownBaseName(exercise)}.md`;
}

function driveMarkdownBaseName(exercise) {
  return String(exercise.slug || exercise.title || "ejercicio")
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "") || "ejercicio";
}

function withSourceFooter(markdown, provider, exercise) {
  const clean = stripSourceSection(String(markdown || "").trim());
  const footer = [
    "",
    "## Fuente",
    "",
    `- Proveedor: ${provider}`,
    exercise.sourceUrl ? `- URL: ${exercise.sourceUrl}` : "",
  ].filter(Boolean).join("\n");
  return `${clean}\n${footer}\n`;
}

function stripSourceSection(markdown) {
  const match = markdown.search(/^##\s+(Fuente|Source)\b/im);
  return match >= 0 ? markdown.slice(0, match).trim() : markdown.trim();
}

function fallbackMarkdown(provider, exercise) {
  const topics = Array.isArray(exercise.topics) ? exercise.topics.join(", ") : "";
  return `# ${exercise.title}

${exercise.blurb || "Resuelve este ejercicio en C."}

## Objetivo

Resuelve este ejercicio dentro de Estudio Socratico.

## Temas

${topics || "fundamentos"}

## Fuente

${provider}
`;
}
