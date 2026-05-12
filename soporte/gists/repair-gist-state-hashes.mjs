import fs from 'fs';
import path from 'path';
import crypto from 'crypto';

const GISTS_STATE_DIR = '.generated/gists';
const STATE_FILE = path.join(GISTS_STATE_DIR, 'alejandro-gist-state.json');
const EXERCISES_DIR = '.generated/alejandro-exercises';
const CATALOG_FILE = path.join(EXERCISES_DIR, 'catalog.private.json');

function calculateHash(filesContent) {
    return crypto.createHash('sha256').update(filesContent).digest('hex');
}

let state = JSON.parse(fs.readFileSync(STATE_FILE, 'utf-8'));
const catalog = JSON.parse(fs.readFileSync(CATALOG_FILE, 'utf-8'));

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
    if (typeof existingState === 'string') {
        gistId = existingState;
    } else if (existingState && typeof existingState === 'object') {
        gistId = existingState.id;
    }

    if (gistId) {
        state[ex.id] = { id: gistId, hash: currentHash };
    }
}

fs.writeFileSync(STATE_FILE, JSON.stringify(state, null, 2));
console.log("Hashes updated successfully in state file.");
