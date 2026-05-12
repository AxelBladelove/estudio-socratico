import fs from 'fs';
import path from 'path';

const MANIFEST_FILE = '.generated/gists/alejandro-private-manifest.json';
const EXTENSION_GENERATED_DIR = 'src/generated';
const CATALOG_TS_FILE = path.join(EXTENSION_GENERATED_DIR, 'alejandro-catalog.ts');

function injectManifest() {
    if (!fs.existsSync(MANIFEST_FILE)) {
        console.error(`ERROR: No se encontró el manifest privado en ${MANIFEST_FILE}`);
        console.error("Ejecuta primero el publicador de gists.");
        process.exit(1);
    }

    if (!fs.existsSync(EXTENSION_GENERATED_DIR)) {
        fs.mkdirSync(EXTENSION_GENERATED_DIR, { recursive: true });
    }

    const manifest = JSON.parse(fs.readFileSync(MANIFEST_FILE, 'utf-8'));
    
    // Convert the raw JSON to a TS module
    const tsContent = `// ARCHIVO AUTOGENERADO - NO MODIFICAR
// Este archivo contiene los IDs reales de los Gists y URLs raw.
// NO debe subirse al repositorio público. Está excluido vía .gitignore.

export const ALEJANDRO_CATALOG = ${JSON.stringify(manifest, null, 2)};
`;

    fs.writeFileSync(CATALOG_TS_FILE, tsContent, 'utf-8');
    console.log(`✅ Catálogo inyectado con éxito en ${CATALOG_TS_FILE}`);
}

injectManifest();
