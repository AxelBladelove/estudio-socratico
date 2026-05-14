import { execSync } from 'child_process';

console.log("Starting full semantic curation pipeline...");
try {
  console.log("1. Running AI Semantic Curation...");
  execSync('node _estudio/soporte/alejandro/semantic-curate-alejandro.mjs', { stdio: 'inherit' });

  console.log("2. Downloading Icons...");
  execSync('npm run alejandro:icons', { stdio: 'inherit' });

  console.log("3. Generating Report...");
  execSync('npm run alejandro:semantic:report', { stdio: 'inherit' });

  console.log("4. Applying Results to Local Files...");
  execSync('npm run alejandro:semantic:apply', { stdio: 'inherit' });

  console.log("Pipeline finished successfully!");
} catch (e) {
  console.error("Pipeline failed!", e.message);
}
