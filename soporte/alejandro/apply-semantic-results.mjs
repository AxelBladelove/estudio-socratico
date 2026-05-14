import fs from 'fs/promises';
import path from 'path';

const OUT_DIR = path.join(process.cwd(), '.generated', 'alejandro-semantic');
const RESULTS_FILE = path.join(OUT_DIR, 'semantic-results.json');
const OVERRIDES_FILE = path.join(OUT_DIR, 'manual-overrides.json');
const EXERCISES_DIR = path.join(process.cwd(), '.generated', 'alejandro-exercises');

async function main() {
  let results = {};
  let overrides = {};

  try {
    results = JSON.parse(await fs.readFile(RESULTS_FILE, 'utf8'));
  } catch (e) {
    console.error("No semantic results found. Run alejandro:semantic first.");
    process.exit(1);
  }

  try {
    overrides = JSON.parse(await fs.readFile(OVERRIDES_FILE, 'utf8'));
  } catch (e) {
    console.log("No manual overrides found or invalid JSON. Skipping overrides.");
  }

  let processedCount = 0;

  for (const slug of Object.keys(results)) {
    const targetDir = path.join(EXERCISES_DIR, slug);
    let dirExists = true;
    try { await fs.access(targetDir); } catch(e) { dirExists = false; }
    if (!dirExists) continue;

    const data = results[slug];
    const override = overrides[slug] || {};

    const finalTitle = override.title || data.title;
    const finalDescription = override.shortDescription || data.shortDescription;
    const finalFilters = override.filters || data.filters;
    const finalDifficulty = override.difficulty || data.difficulty;
    const finalTopics = override.topics || data.topics;

    const metaPath = path.join(targetDir, 'metadata.json');
    let currentMeta = {};
    try {
      currentMeta = JSON.parse(await fs.readFile(metaPath, 'utf8'));
    } catch(e) {
      console.error(`Skipping ${slug}, invalid metadata.json`);
      continue;
    }

    currentMeta.title = finalTitle;
    
    // Asignar en filters.topic y filters.difficulty para mantener compatibilidad 
    // con el catálogo existente y manager.ps1 si asume esa estructura.
    if (!currentMeta.filters) currentMeta.filters = {};
    
    currentMeta.filters.topic = finalFilters; // Actualizamos los filtros 
    currentMeta.filters.difficulty = finalDifficulty;
    currentMeta.filters.topicsTechnical = finalTopics; // Guardamos los topics técnicos aparte
    currentMeta.shortDescription = finalDescription;

    await fs.writeFile(metaPath, JSON.stringify(currentMeta, null, 2));
    processedCount++;
  }

  console.log(`Successfully applied metadata to ${processedCount} exercises.`);
}

main().catch(err => {
  console.error(err);
  process.exit(1);
});
