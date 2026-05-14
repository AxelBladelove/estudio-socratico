import fs from 'fs/promises';
import path from 'path';
import os from 'os';
import { execSync } from 'child_process';

const EXERCISES_DIR = path.join(process.cwd(), '.generated', 'alejandro-exercises');
const OUT_DIR = path.join(process.cwd(), '.generated', 'alejandro-semantic');
const RESULTS_FILE = path.join(OUT_DIR, 'semantic-results.json');
const LOW_CONFIDENCE_FILE = path.join(OUT_DIR, 'low-confidence.json');
const REPORT_FILE = path.join(OUT_DIR, 'semantic-review-report.md');
const TAXONOMY_FILE = path.join(process.cwd(), 'soporte', 'alejandro', 'semantic-taxonomy.json');

async function ensureDir(dir) {
  try {
    await fs.mkdir(dir, { recursive: true });
  } catch (e) {}
}

async function callAI(prompt, systemInstruction = "") {
  return new Promise(async (resolve, reject) => {
    try {
      const fullPrompt = systemInstruction ? `[System Instruction]\n${systemInstruction}\n\n[Task]\n${prompt}` : prompt;
      
      const tmpFile = path.join(os.tmpdir(), `opencode_prompt_${Date.now()}.txt`);
      await fs.writeFile(tmpFile, fullPrompt, 'utf8');
      
      // Using deepseek v4 since it is one of the best free models for JSON
      const cmd = `cmd.exe /c "opencode run -m opencode/deepseek-v4-flash-free \\"Por favor resuelve la tarea indicada en el archivo adjunto devolviendo SOLO EL JSON.\\" -f ${tmpFile} --format json"`;
      
      const res = execSync(cmd, { encoding: 'utf8', stdio: 'pipe' });
      
      let fullText = '';
      const lines = res.split('\n');
      for (const line of lines) {
        if (!line.trim()) continue;
        try {
          const event = JSON.parse(line);
          if (event.type === 'text' && event.part && event.part.text) {
             fullText += event.part.text;
          }
        } catch(e) {}
      }
      
      resolve(fullText);
    } catch(e) {
       let errorMsg = e.message;
       if (e.stderr) errorMsg += "\nSTDERR: " + e.stderr.toString();
       if (e.stdout) errorMsg += "\nSTDOUT: " + e.stdout.toString();
       reject(new Error(errorMsg));
    }
  });
}

function validateSchema(data, taxonomy) {
  const errors = [];
  if (!data.title || typeof data.title !== 'string' || data.title.length === 0) errors.push("title is empty or not string");
  if (!data.shortDescription || typeof data.shortDescription !== 'string' || data.shortDescription.length === 0) errors.push("shortDescription is empty or not string");
  if (data.shortDescription && data.shortDescription.length > 110) errors.push("shortDescription exceeds 110 chars");
  
  if (!Array.isArray(data.filters) || data.filters.length < 2 || data.filters.length > 6) {
    errors.push("filters must be an array of length 2 to 6");
  } else {
    for (const f of data.filters) {
      if (!taxonomy.filters.includes(f)) errors.push(`filter '${f}' not in taxonomy`);
    }
  }

  if (typeof data.confidence !== 'number' || data.confidence < 0.65) errors.push("confidence is below 0.65 or not a number");
  
  return errors;
}

async function main() {
  const args = process.argv.slice(2);
  let limit = Infinity;
  for (let i = 0; i < args.length; i++) {
    if (args[i] === '--limit' && args[i+1]) {
      limit = parseInt(args[i+1], 10);
    }
  }

  await ensureDir(OUT_DIR);
  
  const taxonomy = JSON.parse(await fs.readFile(TAXONOMY_FILE, 'utf8'));
  
  let results = {};
  let lowConfidence = {};
  
  try {
    results = JSON.parse(await fs.readFile(RESULTS_FILE, 'utf8'));
  } catch (e) {}
  
  try {
    lowConfidence = JSON.parse(await fs.readFile(LOW_CONFIDENCE_FILE, 'utf8'));
  } catch (e) {}

  const dirs = await fs.readdir(EXERCISES_DIR);
  let processedCount = 0;

  for (const dir of dirs) {
    if (processedCount >= limit) break;
    
    const slug = dir;
    if (slug.startsWith('.')) continue; // ignore hidden
    
    if (results[slug]) {
      console.log(`Skipping already processed: ${slug}`);
      continue;
    }
    
    const metaPath = path.join(EXERCISES_DIR, slug, 'metadata.json');
    const instPath = path.join(EXERCISES_DIR, slug, 'instructions.md');
    
    let metaExists = true;
    try { await fs.access(metaPath); } catch(e) { metaExists = false; }
    if (!metaExists) continue;

    console.log(`Processing: ${slug}`);
    
    const metaContent = await fs.readFile(metaPath, 'utf8');
    const instContent = await fs.readFile(instPath, 'utf8');
    const currentMeta = JSON.parse(metaContent);
    
    const systemInstruction = `Eres un experto curador técnico para una plataforma educativa estilo Exercism.
Tu trabajo es generar metadata JSON estricta para ejercicios de programación en C.
Debes devolver SOLO JSON sin markdown u otros comentarios.`;

    const prompt = `Analiza este ejercicio de programación en C. Tu trabajo no es resolverlo. Tu trabajo es crear metadata para una extensión educativa tipo Exercism. Genera un título humano (máximo 55 caracteres), una descripción corta (máximo 110 caracteres), filtros y topics basados solo en lo que el ejercicio realmente pide.
No uses la sección del PDF como evidencia suficiente. No inventes filtros. Si el ejercicio menciona matrices, usa matrices; si no, no. Si menciona punteros, usa punteros; si no, no. Devuelve JSON estricto.

Taxonomía de filtros permitidos:
${JSON.stringify(taxonomy.filters)}

Dificultades permitidas:
${JSON.stringify(taxonomy.difficulties)}

Schema obligatorio:
{
  "slug": "${slug}",
  "title": "string",
  "shortDescription": "string",
  "difficulty": "beginner|easy|medium|hard|challenge",
  "filters": ["string"],
  "topics": ["string"],
  "iconSearchTerms": ["string"],
  "iconConcept": "string",
  "reasoningSummary": "string",
  "confidence": 0.0
}

Reglas:
- title: máximo 55 chars.
- shortDescription: máximo 110 chars.
- filters: solo valores exactos de la taxonomía permitida. Entre 2 y 6 filtros.
- topics: técnicos, máximo 8, en kebab-case.
- iconSearchTerms: 3 a 8 términos en inglés o español útiles para buscar iconos en Iconify.
- iconConcept: una frase corta, por ejemplo "calendar date", "chess queen".
- reasoningSummary: breve explicación de por qué asignó esos filtros.
- confidence: número entre 0 y 1.

Instrucción actual del ejercicio:
--------------------------------
${instContent}
--------------------------------

Título original extraído: ${currentMeta.title || ''}`;

    let success = false;
    let retries = 1;
    let lastError = null;

    while (retries >= 0 && !success) {
      try {
        let currentPrompt = prompt;
        if (lastError) {
          currentPrompt += `\n\nATENCIÓN, tu respuesta anterior falló la validación por el siguiente error: ${lastError}. Por favor, corrige esto y devuelve un JSON válido que cumpla estrictamente con las reglas.`;
        }
        
        const aiResponse = await callAI(currentPrompt, systemInstruction);
        // Extraer JSON si el modelo devuelve markdown backticks
        const cleanJson = aiResponse.replace(/^\s*```json\n?/, '').replace(/```\s*$/, '');
        const data = JSON.parse(cleanJson);
        
        // Forzar slug
        data.slug = slug;
        
        const errors = validateSchema(data, taxonomy);
        if (errors.length > 0) {
          throw new Error("Validation failed: " + errors.join("; "));
        }
        
        results[slug] = data;
        if (lowConfidence[slug]) delete lowConfidence[slug];
        success = true;
        console.log(`  -> Success! Confidence: ${data.confidence}, Filters: ${data.filters.join(', ')}`);
        
      } catch (err) {
        lastError = err.message;
        console.log(`  -> Error: ${err.message}. Retries left: ${retries}`);
        retries--;
      }
    }

    if (!success) {
      console.log(`  -> Failed after retries for ${slug}. Logging to low-confidence.`);
      lowConfidence[slug] = {
        error: lastError,
        timestamp: new Date().toISOString()
      };
    }
    
    processedCount++;
    // Save state iteratively
    await fs.writeFile(RESULTS_FILE, JSON.stringify(results, null, 2));
    await fs.writeFile(LOW_CONFIDENCE_FILE, JSON.stringify(lowConfidence, null, 2));
    
    if (processedCount < limit) {
      console.log("  -> Waiting 1.5s between opencode calls...");
      await new Promise(r => setTimeout(r, 1500));
    }
  }
  
  console.log(`Finished processing ${processedCount} exercises.`);
}

main().catch(err => {
  console.error(err);
  process.exit(1);
});
