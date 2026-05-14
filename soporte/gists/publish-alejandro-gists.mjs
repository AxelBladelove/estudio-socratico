import fs from 'fs';
import path from 'path';
import { execSync } from 'child_process';
import crypto from 'crypto';

/**
 * Estudio Socrático - Gist Publisher 2.1
 */

const GISTS_STATE_DIR = '.generated/gists';
const STATE_FILE = path.join(GISTS_STATE_DIR, 'alejandro-gist-state.json');
const MANIFEST_FILE = path.join(GISTS_STATE_DIR, 'alejandro-private-manifest.json');
const REPORT_MD = path.join(GISTS_STATE_DIR, 'alejandro-gist-sync-report.md');
const REPORT_JSON = path.join(GISTS_STATE_DIR, 'alejandro-gist-sync-report.json');
const EXERCISES_DIR = '.generated/alejandro-exercises';
const CATALOG_FILE = path.join(EXERCISES_DIR, 'catalog.private.json');

// --- Utilidades ---

function getAuthToken() {
    if (process.env.GITHUB_TOKEN) return process.env.GITHUB_TOKEN;
    try {
        return execSync('gh auth token', { stdio: ['pipe', 'pipe', 'ignore'] }).toString().trim();
    } catch (e) { return null; }
}

function calculateHash(filesContent) {
    return crypto.createHash('sha256').update(filesContent).digest('hex');
}

async function sleep(ms) {
    return new Promise(r => setTimeout(r, ms));
}

function parseArgs() {
    const args = process.argv.slice(2);
    const getVal = (flag) => {
        const idx = args.indexOf(flag);
        return idx !== -1 ? args[idx + 1] : null;
    };
    
    return {
        dryRun: args.includes('--dry-run'),
        onlyPending: args.includes('--only-pending'),
        limit: parseInt(getVal('--limit')) || 999,
        delay: parseInt(getVal('--delay')) || 3000,
        verifyRemote: args.includes('--verify-remote'),
        verifyLimit: parseInt(getVal('--verify-limit')) || 10
    };
}

// --- Lógica Principal ---

async function run() {
    const options = parseArgs();
    const token = getAuthToken();
    let githubUsername = "AxelBladelove";

    if (!token && !options.dryRun) {
        console.error("ERROR: No se encontró autenticación para GitHub. Usa 'gh auth login' o GITHUB_TOKEN.");
        process.exit(1);
    }

    // Intentar obtener el usuario real de GitHub
    if (token) {
        try {
            const userRes = await fetch('https://api.github.com/user', {
                headers: { 'Authorization': `token ${token}`, 'Accept': 'application/vnd.github.v3+json' }
            });
            if (userRes.ok) {
                const userData = await userRes.json();
                githubUsername = userData.login;
            }
        } catch (e) {
            console.warn("⚠️ No se pudo obtener el username de GitHub, usando fallback.");
        }
    }

    if (!fs.existsSync(GISTS_STATE_DIR)) fs.mkdirSync(GISTS_STATE_DIR, { recursive: true });

    if (!fs.existsSync(CATALOG_FILE)) {
        console.error(`ERROR: No se encontró el catálogo en ${CATALOG_FILE}`);
        process.exit(1);
    }
    const catalog = JSON.parse(fs.readFileSync(CATALOG_FILE, 'utf-8'));

    let state = {};
    if (fs.existsSync(STATE_FILE)) {
        state = JSON.parse(fs.readFileSync(STATE_FILE, 'utf-8'));
    }

    const report = {
        totalExercises: catalog.length,
        syncedRemote: 0,
        pendingRemote: 0,
        failedRemote: 0,
        manifestOnly: 0,
        skippedBecauseHashMatched: 0,
        updatedSuccessfully: 0,
        blockedByRateLimit: 0,
        exercises: []
    };

    const manifest = {
        schemaVersion: 1,
        source: "alejandro",
        displayName: "Problemas de Programación",
        language: "c",
        generatedAt: new Date().toISOString(),
        exerciseCount: catalog.length,
        exercises: []
    };

    console.log(`\n🔍 Auditando ${catalog.length} ejercicios...`);
    if (options.dryRun) console.log("⚠️ MODO DRY-RUN ACTIVADO\n");

    let processedCount = 0;
    let verifiedCount = 0;

    for (const ex of catalog) {
        const exDir = path.join(EXERCISES_DIR, ex.id);
        if (!fs.existsSync(exDir)) continue;

        const metadata = JSON.parse(fs.readFileSync(path.join(exDir, 'metadata.json'), 'utf-8'));
        const instructions = fs.readFileSync(path.join(exDir, 'instructions.md'), 'utf-8');
        const icon = fs.readFileSync(path.join(exDir, 'icon.svg'), 'utf-8');
        
        let filesContentToHash = instructions + JSON.stringify(metadata) + icon;
        ['starter.c', 'README.md'].forEach(f => {
            const p = path.join(exDir, f);
            if (fs.existsSync(p)) filesContentToHash += fs.readFileSync(p, 'utf-8');
        });

        const localHash = calculateHash(filesContentToHash);
        let entry = state[ex.id] || {};
        if (typeof entry === 'string') entry = { id: entry };
        
        // Upgrade state schema
        entry.slug = ex.id.replace('alejandro-', '');
        entry.gistId = entry.id || entry.gistId || null;
        entry.localHash = localHash;
        entry.lastSuccessfulRemoteHash = entry.lastSuccessfulRemoteHash || entry.hash || null;
        
        // -- VERIFICACIÓN REMOTA --
        if (options.verifyRemote && entry.gistId && verifiedCount < options.verifyLimit) {
            console.log(`[VERIFY] Comprobando Gist ${entry.gistId} para ${ex.id}...`);
            try {
                const vRes = await fetch(`https://api.github.com/gists/${entry.gistId}`, {
                    headers: { 'Authorization': `token ${token}`, 'Accept': 'application/vnd.github.v3+json' }
                });
                if (vRes.ok) {
                    const vData = await vRes.json();
                    const remoteMeta = vData.files['metadata.json'] ? JSON.parse(vData.files['metadata.json'].content) : {};
                    const remoteInstr = vData.files['instructions.md'] ? vData.files['instructions.md'].content : "";
                    const remoteIcon = vData.files['icon.svg'] ? vData.files['icon.svg'].content : "";
                    
                    let remoteContent = remoteInstr + JSON.stringify(remoteMeta) + remoteIcon;
                    // TODO: handle starter.c if needed for hash verification
                    
                    const remoteHash = calculateHash(remoteContent);
                    if (remoteHash === localHash) {
                        entry.lastSuccessfulRemoteHash = localHash;
                        entry.remoteSyncStatus = "synced";
                        console.log(`  ✅ Verificado: Sincronizado.`);
                    } else {
                        entry.remoteSyncStatus = "pending";
                        console.warn(`  ⚠️ Verificado: Desincronizado (Remote Hash != Local Hash).`);
                    }
                    verifiedCount++;
                } else {
                    console.error(`  ❌ Error verificando: ${vRes.status}`);
                }
            } catch (e) {
                console.error(`  ❌ Error de red verificando: ${e.message}`);
            }
        }

        // Determinar status actual si no se ha verificado remotamente ahora
        if (!entry.remoteSyncStatus || entry.remoteSyncStatus === "synced") {
            if (entry.gistId && entry.lastSuccessfulRemoteHash === localHash) {
                entry.remoteSyncStatus = "synced";
            } else {
                entry.remoteSyncStatus = "pending";
            }
        }

        const isSynced = entry.remoteSyncStatus === "synced";
        const needsUpdate = !isSynced;

        if (isSynced) {
            report.syncedRemote++;
            report.skippedBecauseHashMatched++;
        } else {
            report.pendingRemote++;
        }

        // -- PROCESAR --
        let actionTaken = false;
        if ((!options.onlyPending || needsUpdate) && processedCount < options.limit) {
            if (options.dryRun) {
                if (needsUpdate) {
                    console.log(`[DRY-RUN] ${entry.gistId ? 'Actualizaría' : 'Crearía'} ${ex.id}`);
                    processedCount++;
                }
            } else if (needsUpdate) {
                processedCount++;
                actionTaken = true;
                entry.lastAttemptAt = new Date().toISOString();

                const payload = {
                    description: `Estudio Socrático — Alejandro — ${ex.title}`,
                    public: false,
                    files: {
                        "instructions.md": { content: instructions },
                        "metadata.json": { content: JSON.stringify(metadata, null, 2) },
                        "icon.svg": { content: icon }
                    }
                };

                const url = entry.gistId ? `https://api.github.com/gists/${entry.gistId}` : 'https://api.github.com/gists';
                const method = entry.gistId ? 'PATCH' : 'POST';

                console.log(`[${method}] ${ex.id} (${processedCount}/${options.limit})...`);
                try {
                    const response = await fetch(url, {
                        method,
                        headers: {
                            'Authorization': `token ${token}`,
                            'Accept': 'application/vnd.github.v3+json',
                            'Content-Type': 'application/json'
                        },
                        body: JSON.stringify(payload)
                    });

                    // Rate Limit Headers
                    const remaining = response.headers.get('x-ratelimit-remaining');
                    const reset = response.headers.get('x-ratelimit-reset');
                    const retryAfter = response.headers.get('retry-after');

                    if (response.status === 403 || response.status === 429) {
                        const body = await response.text();
                        console.warn(`\n⚠️ GitHub Rate Limit: ${response.status}`);
                        entry.remoteSyncStatus = "rate-limited";
                        entry.lastError = body;
                        report.blockedByRateLimit++;

                        if (retryAfter) {
                            const wait = parseInt(retryAfter) * 1000;
                            console.warn(`Retry-After: ${retryAfter}s. Esperando...`);
                            await sleep(wait + 1000);
                        } else if (remaining === "0") {
                            const wait = (parseInt(reset) * 1000) - Date.now() + 2000;
                            console.warn(`Límite agotado. Reset en ${Math.round(wait/1000)}s.`);
                            if (wait > 0 && wait < 60000) await sleep(wait);
                            else { console.error("Reset muy lejano. Abortando."); break; }
                        } else {
                            console.error("Secondary Rate Limit detectado. Abortando.");
                            break;
                        }
                    } else if (!response.ok) {
                        entry.remoteSyncStatus = "failed";
                        entry.lastError = `HTTP ${response.status}: ${response.statusText}`;
                        report.failedRemote++;
                        console.error(`❌ Falló: ${entry.lastError}`);
                    } else {
                        const data = await response.json();
                        entry.gistId = data.id;
                        entry.id = data.id;
                        entry.lastSuccessfulRemoteHash = localHash;
                        entry.remoteSyncStatus = "synced";
                        entry.lastSuccessAt = new Date().toISOString();
                        entry.lastError = null;
                        report.updatedSuccessfully++;
                        report.syncedRemote++;
                        report.pendingRemote--;
                        console.log(`  ✅ Sincronizado: ${entry.gistId}`);
                    }
                } catch (e) {
                    entry.remoteSyncStatus = "failed";
                    entry.lastError = e.message;
                    report.failedRemote++;
                    console.error(`  ❌ Error de red: ${e.message}`);
                }
                
                await sleep(options.delay);
            }
        }

        if (entry.remoteSyncStatus !== "synced" && entry.gistId) {
            report.manifestOnly++;
        }

        state[ex.id] = entry;

        // Manifiesto (con metadatos locales siempre)
        manifest.exercises.push({
            id: ex.id,
            slug: entry.slug,
            title: ex.title,
            description: metadata.shortDescription || `Conceptos de ${metadata.filters.topic.join(', ')}.`,
            difficulty: metadata.filters.difficulty,
            topics: metadata.filters.topic,
            filters: (metadata.filters.topic || []).concat(["programacion-basica", metadata.filters.kind || "programa"]),
            gistId: entry.gistId || "PENDING",
            remoteSyncStatus: entry.remoteSyncStatus,
            files: {
                instructions: `https://gist.githubusercontent.com/${githubUsername}/${entry.gistId || 'PENDING'}/raw/instructions.md`,
                metadata: `https://gist.githubusercontent.com/${githubUsername}/${entry.gistId || 'PENDING'}/raw/metadata.json`,
                icon: `https://gist.githubusercontent.com/${githubUsername}/${entry.gistId || 'PENDING'}/raw/icon.svg`
            }
        });

        if (!options.dryRun && actionTaken) {
            fs.writeFileSync(STATE_FILE, JSON.stringify(state, null, 2));
        }
    }

    if (!options.dryRun) {
        fs.writeFileSync(STATE_FILE, JSON.stringify(state, null, 2));
        fs.writeFileSync(MANIFEST_FILE, JSON.stringify(manifest, null, 2));
    }

    report.exercises = Object.keys(state).map(id => ({
        id,
        gistId: state[id].gistId,
        status: state[id].remoteSyncStatus,
        lastError: state[id].lastError
    }));

    fs.writeFileSync(REPORT_JSON, JSON.stringify(report, null, 2));
    
    const pendingSlugs = report.exercises.filter(e => e.status !== 'synced').map(e => e.id);
    const reportMd = `# Reporte de Sincronización de Gists - Alejandro\n\nFecha: ${new Date().toLocaleString()}\n\n## Resumen\n- Total: ${report.totalExercises}\n- Sincronizados: ${report.syncedRemote}\n- Pendientes: ${report.pendingRemote}\n- Bloqueados: ${report.blockedByRateLimit}\n- Fallidos: ${report.failedRemote}\n\n## Pendientes\n${pendingSlugs.length > 0 ? pendingSlugs.map(s => `- \`${s}\``).join('\n') : '¡Todo OK!'}`;
    fs.writeFileSync(REPORT_MD, reportMd);

    console.log(`\n📊 Reporte: ${REPORT_MD}`);
    console.log(`✅ Sincronizados: ${report.syncedRemote}/${report.totalExercises}`);
    if (report.pendingRemote > 0) console.warn(`⏳ Pendientes:    ${report.pendingRemote}`);
}

run().catch(console.error);
