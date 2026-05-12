import fs from 'fs';
import path from 'path';

const MANIFEST_FILE = '.generated/gists/alejandro-private-manifest.json';

// Destinations
const TS_DIR = 'src/generated';
const TS_FILE = path.join(TS_DIR, 'alejandro-catalog.ts');
const MANAGER_CATALOG = 'soporte/exercism/catalogs/alejandro.json';
const EXTENSION_CATALOG = 'soporte/vscode/estudio-exercism/generated/alejandro-catalog.json';

function injectManifest() {
    if (!fs.existsSync(MANIFEST_FILE)) {
        console.error(`ERROR: No se encontró el manifest privado en ${MANIFEST_FILE}`);
        console.error("Ejecuta primero: npm run alejandro:gists:publish");
        process.exit(1);
    }

    const manifest = JSON.parse(fs.readFileSync(MANIFEST_FILE, 'utf-8'));
    const exercises = manifest.exercises || [];

    // ────────────────────────────────────────────────────────
    // 1. Generate the static catalog that manager.ps1 reads
    //    (soporte/exercism/catalogs/alejandro.json)
    //    This replaces the old Drive-based catalog entirely.
    // ────────────────────────────────────────────────────────
    const managerCatalog = {
        provider: "alejandro",
        providerName: "PDF Alejandro Liz",
        generatedAt: manifest.generatedAt,
        exercises: exercises.map((ex, index) => ({
            slug: ex.id,                       // e.g. "alejandro-imprimir-nombre-n-veces"
            title: ex.title,                   // e.g. "Imprimir nombre N veces"
            folderName: ex.title,              // Clean human title, not slug
            difficulty: mapDifficulty(ex.difficulty),
            blurb: ex.description || "",
            topics: ex.topics || [],
            iconUrl: ex.files?.icon || "",
            order: index + 1,
            sourceUrl: "Problemas de Programación — Rolando J. Batista & Alejandro J. Liz",
            gistInstructionsUrl: ex.files?.instructions || "",
            instructionMarkdown: "",           // Will be downloaded at import time from Gist
            supportsTests: false,
            supportsSubmit: false,
        })),
    };

    fs.mkdirSync(path.dirname(MANAGER_CATALOG), { recursive: true });
    fs.writeFileSync(MANAGER_CATALOG, JSON.stringify(managerCatalog, null, 2), 'utf-8');
    console.log(`✅ Catálogo para manager.ps1 → ${MANAGER_CATALOG} (${managerCatalog.exercises.length} ejercicios)`);

    // ────────────────────────────────────────────────────────
    // 2. Copy the full manifest into the extension directory
    //    (soporte/vscode/estudio-exercism/generated/alejandro-catalog.json)
    // ────────────────────────────────────────────────────────
    fs.mkdirSync(path.dirname(EXTENSION_CATALOG), { recursive: true });
    fs.writeFileSync(EXTENSION_CATALOG, JSON.stringify(manifest, null, 2), 'utf-8');
    console.log(`✅ Catálogo para extensión → ${EXTENSION_CATALOG}`);

    // ────────────────────────────────────────────────────────
    // 3. Generate the TS reference module (auxiliary, gitignored)
    // ────────────────────────────────────────────────────────
    fs.mkdirSync(TS_DIR, { recursive: true });
    const tsContent = `// ARCHIVO AUTOGENERADO - NO MODIFICAR
// Este archivo contiene los IDs reales de los Gists y URLs raw.
// NO debe subirse al repositorio público. Está excluido vía .gitignore.

export const ALEJANDRO_CATALOG = ${JSON.stringify(manifest, null, 2)};
`;
    fs.writeFileSync(TS_FILE, tsContent, 'utf-8');
    console.log(`✅ Catálogo TS de referencia → ${TS_FILE}`);
}

function mapDifficulty(raw) {
    const map = {
        "basico": "easy",
        "básico": "easy",
        "intermedio": "medium",
        "avanzado": "hard",
    };
    return map[(raw || "").toLowerCase()] || raw || "medium";
}

injectManifest();
