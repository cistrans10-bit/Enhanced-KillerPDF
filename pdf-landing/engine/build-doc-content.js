const fs = require('fs');
const path = require('path');

const docsDirectory = path.join(__dirname, 'docs');
const documents = {};

for (const file of fs.readdirSync(docsDirectory).filter(file => file.endsWith('.md')).sort()) {
  documents[path.basename(file, '.md')] = fs.readFileSync(path.join(docsDirectory, file), 'utf8');
}

const output = '(function(){window.KPDF_ENGINE_DOCS=' + JSON.stringify(documents) + ';})();\n';
fs.writeFileSync(path.join(__dirname, 'engine-docs-content.js'), output, 'utf8');
