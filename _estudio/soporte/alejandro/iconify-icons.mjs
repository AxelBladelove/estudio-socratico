import fs from 'fs/promises';
import path from 'path';
import https from 'https';

const OUT_DIR = path.join(process.cwd(), '.generated', 'alejandro-semantic');
const RESULTS_FILE = path.join(OUT_DIR, 'semantic-results.json');
const EXERCISES_DIR = path.join(process.cwd(), '.generated', 'alejandro-exercises');

const PREFERRED_COLLECTIONS = ['lucide', 'tabler', 'solar', 'phosphor', 'mdi', 'carbon', 'bx'];

async function fetchJSON(url) {
  return new Promise((resolve, reject) => {
    https.get(url, (res) => {
      let data = '';
      res.on('data', chunk => data += chunk);
      res.on('end', () => {
        if (res.statusCode >= 200 && res.statusCode < 300) {
          try {
            resolve(JSON.parse(data));
          } catch (e) {
            reject(e);
          }
        } else {
          reject(new Error(`HTTP ${res.statusCode}: ${data}`));
        }
      });
    }).on('error', reject);
  });
}

async function fetchText(url) {
  return new Promise((resolve, reject) => {
    https.get(url, (res) => {
      let data = '';
      res.on('data', chunk => data += chunk);
      res.on('end', () => {
        if (res.statusCode >= 200 && res.statusCode < 300) {
          resolve(data);
        } else {
          reject(new Error(`HTTP ${res.statusCode}: ${data}`));
        }
      });
    }).on('error', reject);
  });
}

// Sanitiza muy básico
function sanitizeSVG(svg) {
  let clean = svg.replace(/<script\b[^<]*(?:(?!<\/script>)<[^<]*)*<\/script>/gi, '');
  clean = clean.replace(/on\w+="[^"]*"/g, '');
  clean = clean.replace(/<foreignObject\b[^<]*(?:(?!<\/foreignObject>)<[^<]*)*<\/foreignObject>/gi, '');
  clean = clean.replace(/<image\b[^<]*(?:(?!<\/image>)<[^<]*)*<\/image>/gi, '');
  return clean;
}

async function main() {
  const args = process.argv.slice(2);
  let limit = Infinity;
  for (let i = 0; i < args.length; i++) {
    if (args[i] === '--limit' && args[i+1]) {
      limit = parseInt(args[i+1], 10);
    }
  }

  let results = {};
  try {
    results = JSON.parse(await fs.readFile(RESULTS_FILE, 'utf8'));
  } catch (e) {
    console.error("No semantic results found. Run alejandro:semantic first.");
    process.exit(1);
  }

  let processedCount = 0;

  for (const slug of Object.keys(results)) {
    if (processedCount >= limit) break;
    const data = results[slug];
    
    // Check if we already fetched icon for this semantic run. We will save it alongside metadata for now.
    // Or we just fetch and save directly in the `.generated/alejandro-exercises/<slug>` folder to not clutter the semantic folder.
    // Wait, the prompt says: "guardar como icon.svg y guardar metadata del icono usado".
    // Phase 6: "Guardar por ejercicio: icon.svg, icon-metadata.json".
    // So we save it directly in `.generated/alejandro-exercises/<slug>/`
    const targetDir = path.join(EXERCISES_DIR, slug);
    let dirExists = true;
    try { await fs.access(targetDir); } catch(e) { dirExists = false; }
    if (!dirExists) continue;

    console.log(`Fetching icon for: ${slug}`);
    
    let terms = data.iconSearchTerms || [];
    if (terms.length === 0) terms = [data.iconConcept || 'code'];
    
    let chosenIcon = null;
    let chosenQuery = '';
    
    for (const term of terms) {
      if (chosenIcon) break;
      const q = encodeURIComponent(term);
      try {
        const searchUrl = `https://api.iconify.design/search?query=${q}&limit=30`;
        const res = await fetchJSON(searchUrl);
        if (res.icons && res.icons.length > 0) {
          // Filtrar por colecciones preferidas
          let found = res.icons.find(i => PREFERRED_COLLECTIONS.includes(i.split(':')[0]));
          if (!found) found = res.icons[0]; // fallback al primero
          
          chosenIcon = found;
          chosenQuery = term;
        }
      } catch(e) {
        console.error(`  -> Search error for term '${term}':`, e.message);
      }
    }
    
    if (!chosenIcon) {
      console.log(`  -> No icon found for any term. Using fallback.`);
      chosenIcon = "lucide:code";
      chosenQuery = "fallback";
    }

    const [prefix, name] = chosenIcon.split(':');
    const svgUrl = `https://api.iconify.design/${prefix}/${name}.svg`;
    
    try {
      const rawSvg = await fetchText(svgUrl);
      const cleanSvg = sanitizeSVG(rawSvg);
      
      const iconMeta = {
        provider: "iconify",
        collection: prefix,
        icon: name,
        sourceQuery: chosenQuery,
        licenseNote: "Iconify open-source icon set; verify collection license if redistributed."
      };
      
      await fs.writeFile(path.join(targetDir, 'icon.svg'), cleanSvg);
      await fs.writeFile(path.join(targetDir, 'icon-metadata.json'), JSON.stringify(iconMeta, null, 2));
      
      console.log(`  -> Saved ${chosenIcon} (query: ${chosenQuery})`);
    } catch(e) {
      console.error(`  -> Error fetching SVG for ${chosenIcon}:`, e.message);
    }
    
    processedCount++;
  }
  
  console.log(`Finished processing ${processedCount} icons.`);
}

main().catch(err => {
  console.error(err);
  process.exit(1);
});
