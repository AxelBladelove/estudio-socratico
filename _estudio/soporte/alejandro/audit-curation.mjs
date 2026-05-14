import fs from 'fs/promises';
import path from 'path';

const EXERCISES_DIR = path.join(process.cwd(), '.generated', 'alejandro-exercises');
const RESULTS_FILE = path.join(process.cwd(), '.generated', 'alejandro-semantic', 'semantic-results.json');
const TAXONOMY_FILE = path.join(process.cwd(), 'soporte', 'alejandro', 'semantic-taxonomy.json');

async function audit() {
  const results = JSON.parse(await fs.readFile(RESULTS_FILE, 'utf8'));
  const taxonomy = JSON.parse(await fs.readFile(TAXONOMY_FILE, 'utf8'));
  
  const totalExercises = Object.keys(results).length;
  const errors = [];
  const warnings = [];
  const titles = new Set();
  const duplicateTitles = [];
  
  const categories = {
    'programacion-basica': [],
    'arreglos-matrices': [],
    'cadenas': [],
    'recursividad': [],
    'estructuras': [],
    'archivos': [],
    'juegos-simulacion': []
  };

  for (const [slug, data] of Object.entries(results)) {
    // 1. Title checks
    if (!data.title) errors.push(`[${slug}] Title is empty`);
    if (data.title && (data.title.toLowerCase().includes('pdf') || data.title.toLowerCase().includes('seccion') || data.title.toLowerCase().includes('página') || data.title.toLowerCase().includes('parte i ejercicio'))) {
      errors.push(`[${slug}] Title contains forbidden PDF markers: "${data.title}"`);
    }
    if (titles.has(data.title)) {
      duplicateTitles.push(data.title);
    }
    titles.add(data.title);

    // 2. ShortDescription checks
    if (!data.shortDescription) errors.push(`[${slug}] shortDescription is empty`);
    if (data.shortDescription && data.shortDescription.length > 110) {
      errors.push(`[${slug}] shortDescription too long (${data.shortDescription.length} chars)`);
    }

    // 3. Difficulty checks
    if (!taxonomy.difficulties.includes(data.difficulty)) {
      errors.push(`[${slug}] Invalid difficulty: ${data.difficulty}`);
    }

    // 4. Filter checks
    if (!Array.isArray(data.filters) || data.filters.length < 2 || data.filters.length > 6) {
      errors.push(`[${slug}] Filters count out of range (2-6): ${data.filters?.length}`);
    }
    for (const f of data.filters || []) {
      if (!taxonomy.filters.includes(f)) {
        errors.push(`[${slug}] Filter not in taxonomy: ${f}`);
      }
    }

    // 5. Confidence checks
    if (data.confidence < 0.7) {
      warnings.push(`[${slug}] Low confidence: ${data.confidence}`);
    }

    // 6. SVG checks
    const svgPath = path.join(EXERCISES_DIR, slug, 'icon.svg');
    try {
      const svg = await fs.readFile(svgPath, 'utf8');
      if (!svg.includes('viewBox')) errors.push(`[${slug}] SVG missing viewBox`);
      if (svg.includes('<script')) errors.push(`[${slug}] SVG contains <script> tag`);
      if (svg.includes('foreignObject')) errors.push(`[${slug}] SVG contains foreignObject`);
      if (svg.includes('base64') && svg.length > 5000) errors.push(`[${slug}] SVG contains large base64 image`);
      if (svg.length > 15000) warnings.push(`[${slug}] SVG suspiciously large: ${svg.length} bytes`);
    } catch (e) {
      errors.push(`[${slug}] icon.svg not found`);
    }

    // Categorization for examples
    if (data.filters.includes('recursividad')) categories['recursividad'].push(slug);
    else if (data.filters.includes('estructuras')) categories['estructuras'].push(slug);
    else if (data.filters.includes('archivos')) categories['archivos'].push(slug);
    else if (data.filters.includes('juegos') || data.filters.includes('simulacion')) categories['juegos-simulacion'].push(slug);
    else if (data.filters.includes('cadenas')) categories['cadenas'].push(slug);
    else if (data.filters.includes('matrices') || data.filters.includes('arreglos')) categories['arreglos-matrices'].push(slug);
    else categories['programacion-basica'].push(slug);
  }

  // Final Output
  const auditResult = {
    totalExercises,
    errorsCount: errors.length,
    warningsCount: warnings.length,
    errors,
    warnings,
    duplicateTitles: [...new Set(duplicateTitles)],
    examples: {
      basica: categories['programacion-basica'].slice(0, 3),
      arreglos: categories['arreglos-matrices'].slice(0, 2),
      cadenas: categories['cadenas'].slice(0, 2),
      recursividad: categories['recursividad'].slice(0, 2),
      estructuras: categories['estructuras'].slice(0, 1),
      archivos: categories['archivos'].slice(0, 1),
      juegos: categories['juegos-simulacion'].slice(0, 1)
    }
  };

  console.log(JSON.stringify(auditResult, null, 2));
}

audit();
