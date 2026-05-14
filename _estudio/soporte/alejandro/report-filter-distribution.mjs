import fs from 'fs/promises';
import path from 'path';

const OUT_DIR = path.join(process.cwd(), '.generated', 'alejandro-semantic');
const RESULTS_FILE = path.join(OUT_DIR, 'semantic-results.json');
const REPORT_FILE = path.join(OUT_DIR, 'filter-distribution.md');

async function main() {
  let results = {};

  try {
    results = JSON.parse(await fs.readFile(RESULTS_FILE, 'utf8'));
  } catch (e) {
    console.error("No semantic results found. Run alejandro:semantic first.");
    process.exit(1);
  }

  const filtersCount = {};
  let totalExercises = 0;
  let tooManyFilters = [];
  let tooFewFilters = [];
  let lowConfidenceList = [];

  for (const [slug, data] of Object.entries(results)) {
    totalExercises++;
    const filters = data.filters || [];
    
    filters.forEach(f => {
      filtersCount[f] = (filtersCount[f] || 0) + 1;
    });

    if (filters.length > 6) tooManyFilters.push(slug);
    if (filters.length < 2) tooFewFilters.push(slug);
    if (data.confidence < 0.75) lowConfidenceList.push({slug, conf: data.confidence});
  }

  const sortedFilters = Object.entries(filtersCount).sort((a, b) => b[1] - a[1]);

  let md = `# Filter Distribution Report\n\n`;
  md += `Total Exercises Processed: **${totalExercises}**\n\n`;

  md += `## Top Filters\n`;
  sortedFilters.forEach(([f, count]) => {
    md += `- **${f}**: ${count}\n`;
  });

  md += `\n## Potential Issues\n`;
  
  if (tooManyFilters.length > 0) {
    md += `### Exercises with >6 filters\n`;
    tooManyFilters.forEach(slug => md += `- ${slug}\n`);
  }

  if (tooFewFilters.length > 0) {
    md += `\n### Exercises with <2 filters\n`;
    tooFewFilters.forEach(slug => md += `- ${slug}\n`);
  }

  if (lowConfidenceList.length > 0) {
    md += `\n### Low Confidence (<0.75)\n`;
    lowConfidenceList.sort((a, b) => a.conf - b.conf).forEach(item => {
      md += `- ${item.slug} (${item.conf})\n`;
    });
  }

  await fs.writeFile(REPORT_FILE, md);
  console.log(`Report generated at ${REPORT_FILE}`);
}

main().catch(err => {
  console.error(err);
  process.exit(1);
});
