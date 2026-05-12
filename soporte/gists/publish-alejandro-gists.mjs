import fs from 'fs';
import path from 'path';
import { execSync } from 'child_process';
import crypto from 'crypto';

const GISTS_STATE_DIR = '.generated/gists';
const STATE_FILE = path.join(GISTS_STATE_DIR, 'alejandro-gist-state.json');
const MANIFEST_FILE = path.join(GISTS_STATE_DIR, 'alejandro-private-manifest.json');
const EXERCISES_DIR = '.generated/alejandro-exercises';
const CATALOG_FILE = path.join(EXERCISES_DIR, 'catalog.private.json');

function getAuthToken() {
    if (process.env.GITHUB_TOKEN) {
        return process.env.GITHUB_TOKEN;
    }
    try {
        return execSync('gh auth token', { stdio: ['pipe', 'pipe', 'ignore'] }).toString().trim();
    } catch (e) {
        return null;
    }
}

function calculateHash(filesContent) {
    return crypto.createHash('sha256').update(filesContent).digest('hex');
}

async function sleep(ms) {
    return new Promise(r => setTimeout(r, ms));
}

async function publishGists() {
    const token = getAuthToken();
    if (!token) {
        console.error("ERROR: No se encontró autenticación para GitHub.");
        console.error("Por favor, ejecuta 'gh auth login' o define la variable GITHUB_TOKEN.");
        process.exit(1);
    }

    if (!fs.existsSync(GISTS_STATE_DIR)) {
        fs.mkdirSync(GISTS_STATE_DIR, { recursive: true });
    }

    let state = {};
    if (fs.existsSync(STATE_FILE)) {
        state = JSON.parse(fs.readFileSync(STATE_FILE, 'utf-8'));
    }

    const catalog = JSON.parse(fs.readFileSync(CATALOG_FILE, 'utf-8'));
    console.log(`Iniciando publicación de ${catalog.length} ejercicios...\n`);

    const manifest = {
        schemaVersion: 1,
        source: "alejandro",
        displayName: "Problemas de Programación",
        language: "c",
        generatedAt: new Date().toISOString(),
        exerciseCount: catalog.length,
        exercises: []
    };

    let counts = {
        created: 0,
        updated: 0,
        skipped: 0,
        failed: 0
    };

    let githubUsername = "AxelBladelove"; 
    try {
        githubUsername = execSync('gh api user -q .login', { stdio: ['pipe', 'pipe', 'ignore'] }).toString().trim();
    } catch(e) {}

    for (const ex of catalog) {
        const exDir = path.join(EXERCISES_DIR, ex.id);
        const metadataRaw = fs.readFileSync(path.join(exDir, 'metadata.json'), 'utf-8');
        const metadata = JSON.parse(metadataRaw);
        const instructions = fs.readFileSync(path.join(exDir, 'instructions.md'), 'utf-8');
        const icon = fs.readFileSync(path.join(exDir, 'icon.svg'), 'utf-8');
        
        let filesContentToHash = instructions + JSON.stringify(metadata) + icon;
        if (fs.existsSync(path.join(exDir, 'starter.c'))) {
            filesContentToHash += fs.readFileSync(path.join(exDir, 'starter.c'), 'utf-8');
        }
        if (fs.existsSync(path.join(exDir, 'README.md'))) {
            filesContentToHash += fs.readFileSync(path.join(exDir, 'README.md'), 'utf-8');
        }

        const currentHash = calculateHash(filesContentToHash);
        const existingState = state[ex.id];
        
        let gistId = null;
        let savedHash = null;
        if (typeof existingState === 'string') {
            gistId = existingState;
        } else if (existingState && typeof existingState === 'object') {
            gistId = existingState.id;
            savedHash = existingState.hash;
        }

        const rawBase = `https://gist.githubusercontent.com/${githubUsername}/${gistId}/raw`;
        
        if (gistId && savedHash === currentHash) {
            console.log(`[SKIPPED] ${ex.id} (Sin cambios)`);
            counts.skipped++;
            
            manifest.exercises.push({
                id: ex.id,
                slug: ex.id.replace('alejandro-', ''),
                title: ex.title,
                description: `Programa que aborda conceptos de ${metadata.filters.topic.join(', ')}.`,
                difficulty: metadata.filters.difficulty,
                topics: metadata.filters.topic,
                filters: metadata.filters.topic.concat(["programacion-basica", metadata.filters.kind]),
                gistId: gistId,
                files: {
                    instructions: `${rawBase}/instructions.md`,
                    metadata: `${rawBase}/metadata.json`,
                    icon: `${rawBase}/icon.svg`
                }
            });
            continue;
        }

        const payload = {
            description: `Estudio Socrático — Alejandro — ${ex.title}`,
            public: false,
            files: {
                "instructions.md": { content: instructions },
                "metadata.json": { content: JSON.stringify(metadata, null, 2) },
                "icon.svg": { content: icon }
            }
        };

        let url = 'https://api.github.com/gists';
        let method = 'POST';
        if (gistId) {
            url = `https://api.github.com/gists/${gistId}`;
            method = 'PATCH';
        }

        let retries = 3;
        let success = false;

        while (retries > 0 && !success) {
            try {
                console.log(`[${method === 'POST' ? 'CREATE' : 'UPDATE'}] ${ex.id}`);
                const response = await fetch(url, {
                    method,
                    headers: {
                        'Authorization': `token ${token}`,
                        'Accept': 'application/vnd.github.v3+json',
                        'Content-Type': 'application/json'
                    },
                    body: JSON.stringify(payload)
                });

                if (response.status === 403) {
                    const limitMsg = await response.text();
                    console.warn(`\n⚠️ Rate limit detectado. GitHub devolvió 403.`);
                    console.warn(`Esperando 30 segundos antes de reintentar... (Intentos restantes: ${retries - 1})`);
                    await sleep(30000); // 30s pause
                    retries--;
                    continue;
                }

                if (!response.ok) {
                    console.error(`\n❌ Error HTTP ${response.status} en ${ex.id}: ${response.statusText}`);
                    counts.failed++;
                    break;
                }

                const data = await response.json();
                
                state[ex.id] = { id: data.id, hash: currentHash };
                fs.writeFileSync(STATE_FILE, JSON.stringify(state, null, 2)); 
                
                const ownerLogin = data.owner ? data.owner.login : githubUsername;
                const stableRawBase = `https://gist.githubusercontent.com/${ownerLogin}/${data.id}/raw`;
                
                manifest.exercises.push({
                    id: ex.id,
                    slug: ex.id.replace('alejandro-', ''),
                    title: ex.title,
                    description: `Programa que aborda conceptos de ${metadata.filters.topic.join(', ')}.`,
                    difficulty: metadata.filters.difficulty,
                    topics: metadata.filters.topic,
                    filters: metadata.filters.topic.concat(["programacion-basica", metadata.filters.kind]),
                    gistId: data.id,
                    files: {
                        instructions: `${stableRawBase}/instructions.md`,
                        metadata: `${stableRawBase}/metadata.json`,
                        icon: `${stableRawBase}/icon.svg`
                    }
                });

                if (method === 'POST') counts.created++;
                else counts.updated++;
                
                success = true;
                await sleep(500); 

            } catch (e) {
                console.error(`\n❌ Error de red procesando ${ex.id}:`, e.message);
                console.warn(`Reintentando en 5 segundos...`);
                await sleep(5000);
                retries--;
            }
        }
        
        if (!success) {
            console.error(`❌ Falló ${ex.id} tras varios intentos.`);
            counts.failed++;
        }
    }

    fs.writeFileSync(MANIFEST_FILE, JSON.stringify(manifest, null, 2));
    
    console.log(`\n=========================================`);
    console.log(`✅ ¡Publicación completada!`);
    console.log(`=========================================`);
    console.log(`Creados:       ${counts.created}`);
    console.log(`Actualizados:  ${counts.updated}`);
    console.log(`Omitidos:      ${counts.skipped}`);
    console.log(`Fallidos:      ${counts.failed}`);
    console.log(`=========================================`);
    console.log(`Manifest privado guardado en: ${MANIFEST_FILE}`);
}

publishGists();
